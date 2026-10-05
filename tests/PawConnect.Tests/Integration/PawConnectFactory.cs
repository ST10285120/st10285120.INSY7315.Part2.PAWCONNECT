using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PawConnect.Tests.Integration;

/// <summary>
/// Boots the real PawConnect web app in memory (same Program.cs, middleware, auth and controllers)
/// using the EF Core in-memory database seeded with the demo data.
/// </summary>
public class PawConnectFactory : WebApplicationFactory<Program>
{
    public const string DemoPassword = "Demo!Paws2026";
    private readonly string _dbName = "it-" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:UseInMemory", "true");
        builder.UseSetting("Database:InMemoryName", _dbName);
        builder.UseSetting("Seed:DemoData", "true");
        builder.UseSetting("Seed:DemoPassword", DemoPassword);
        builder.UseSetting("RateLimiting:AuthPermitPerMinute", "1000");
        builder.UseSetting("Notifications:Retry:InitialBackoffMilliseconds", "0");
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.UseSetting("Logging:LogLevel:Microsoft.EntityFrameworkCore", "Warning");
    }

    /// <summary>HTTPS base address so the Secure auth cookies are sent back, as in production.</summary>
    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    /// <summary>A client authenticated with a bearer token from POST /api/auth/token.</summary>
    public async Task<HttpClient> ApiClientFor(string email)
    {
        var client = Browser();
        var response = await client.PostAsJsonAsync("/api/auth/token", new { email, password = DemoPassword });
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Signs in through the real HTML login form (cookie auth) and returns the page's CSRF token.</summary>
    public static async Task<string> LoginWithCookieAsync(HttpClient client, string email)
    {
        var loginPage = await client.GetStringAsync("/Account/Login");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = DemoPassword,
            ["__RequestVerificationToken"] = ExtractFormToken(loginPage)
        });
        var result = await client.PostAsync("/Account/Login", form);
        if ((int)result.StatusCode != 302) throw new InvalidOperationException($"Login failed: {(int)result.StatusCode}");
        var page = await client.GetStringAsync("/");
        return Regex.Match(page, "<meta name=\"csrf-token\" content=\"([^\"]+)\"").Groups[1].Value;
    }

    public static string ExtractFormToken(string html) =>
        Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

    public record TokenResponse(string TokenType, string AccessToken, int ExpiresIn, string RefreshToken);
}
