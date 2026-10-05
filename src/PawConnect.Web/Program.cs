using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using PawConnect.Core.Entities;
using PawConnect.Infrastructure;
using PawConnect.Infrastructure.Data;
using PawConnect.Web.Security;

var builder = WebApplication.CreateBuilder(args);

// ---------- Logging ----------
// Structured JSON logs outside development. Each line carries the request's TraceId scope,
// so one request can be followed end to end (design doc, section 8.3).
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(o =>
    {
        o.IncludeScopes = true;
        o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
        o.UseUtcTimestamp = true;
    });
}

// ---------- Data, repositories, services ----------
builder.Services.AddPawConnect(builder.Configuration);
builder.Services.AddMemoryCache();

// ---------- Identity (authentication + roles) ----------
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        // Passwords are stored as salted PBKDF2 hashes by ASP.NET Core Identity (never in plain text).
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        // Account lockout after repeated failures (risk register: credential stuffing / brute force).
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders()
    .AddClaimsPrincipalFactory<PawConnectClaimsPrincipalFactory>();

// Re-check the security stamp often so a deactivated user's existing session ends quickly.
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(5));

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    // The API answers 401/403 instead of redirecting to the HTML login page.
    options.Events.OnRedirectToLogin = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api")) ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        else ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api")) ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        else ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
});

// Bearer tokens for API clients (POST /api/auth/token). Built into ASP.NET Core 8.
builder.Services.AddAuthentication()
    .AddBearerToken(IdentityConstants.BearerScheme, o => o.BearerTokenExpiration = TimeSpan.FromHours(1));

builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-CSRF-TOKEN";
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});

// ---------- MVC + API ----------
builder.Services.AddControllersWithViews(options =>
{
    // Every MVC form POST must carry a valid anti-forgery token (CSRF protection).
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    // Unhandled optimistic-concurrency conflicts become a 409 / friendly message, not an error page.
    options.Filters.Add<ConcurrencyConflictFilter>();
})
// Enums travel as names ("UnderReview"); numbers are rejected so a client can't store an undefined value.
.AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddScoped<ApiAntiforgeryFilter>();
builder.Services.AddScoped<BranchAccess>();
builder.Services.AddProblemDetails();

// ---------- Rate limiting on sign-in / registration (OWASP "Insecure Design" mitigation) ----------
var authPermitPerMinute = builder.Configuration.GetValue("RateLimiting:AuthPermitPerMinute", 10);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        return ValueTask.CompletedTask;
    };
    options.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authPermitPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

// ---------- Performance ----------
// Brotli/gzip for CSS, JS and JSON. HTML is deliberately NOT compressed over HTTPS: pages carry
// anti-forgery tokens, and compressing secrets alongside user input enables BREACH-style attacks.
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
    o.MimeTypes = new[] { "text/css", "application/javascript", "text/javascript", "application/json", "application/problem+json", "text/csv", "image/svg+xml" };
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Fastest);

// Azure App Service terminates HTTPS at its front end and forwards the original scheme.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

// ---------- Database migration + seed ----------
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DataSeeder>().RunAsync();
}

// ---------- HTTP pipeline ----------
app.UseForwardedHeaders();
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/api"),
    branch => branch.UseStatusCodePagesWithReExecute("/Home/StatusCode/{0}"));
// API errors without a body (401, 403, 404, 429...) get an RFC 7807 problem+json body too, so
// every API error has the same shape.
app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/api"), branch => branch.UseStatusCodePages());

app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseResponseCompression();
app.UseStaticFiles(new StaticFileOptions
{
    // CSS/JS links carry a content hash (asp-append-version), so browsers can cache them for a year.
    OnPrepareResponse = ctx =>
    {
        if (ctx.Context.Request.Query.ContainsKey("v"))
            ctx.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
    }
});
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
// Which build is running. The deployment pipeline polls this until the new commit is live.
app.MapGet("/version", () => Results.Ok(new
{
    commit = Environment.GetEnvironmentVariable("RENDER_GIT_COMMIT")
             ?? Environment.GetEnvironmentVariable("GIT_COMMIT_SHA")
             ?? BuildCommit()
             ?? "unknown",
    environment = app.Environment.EnvironmentName
}));
app.MapControllers();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// CI builds with -p:SourceRevisionId=<commit>, which .NET appends to the informational version ("1.0.0+<sha>").
static string? BuildCommit()
{
    var version = System.Reflection.Assembly.GetEntryAssembly()?
        .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
        .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion;
    var plus = version?.IndexOf('+') ?? -1;
    return plus >= 0 ? version![(plus + 1)..] : null;
}

/// <summary>Exposed so the integration tests can start the app with WebApplicationFactory.</summary>
public partial class Program { }
