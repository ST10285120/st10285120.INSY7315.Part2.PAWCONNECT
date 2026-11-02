using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;

namespace PawConnect.Tests.Integration;

/// <summary>Two-step verification with an authenticator app (Task 2 feedback: add MFA for admin accounts).</summary>
public class TwoFactorTests : IClassFixture<PawConnectFactory>
{
    private readonly PawConnectFactory _factory;
    public TwoFactorTests(PawConnectFactory factory) => _factory = factory;

    [Fact]
    public async Task After_turning_it_on_a_code_is_needed_to_log_in()
    {
        const string email = "emma@pawconnect.demo";
        var browser = _factory.Browser();
        await PawConnectFactory.LoginWithCookieAsync(browser, email);

        var setup = await browser.GetStringAsync("/Account/TwoFactor");
        Assert.Contains("Set it up in three steps", setup);

        var wrong = await PostForm(browser, setup, "/Account/EnableTwoFactor", new() { ["Code"] = "000000" });
        Assert.Contains("didn&#x27;t match", await wrong.Content.ReadAsStringAsync());

        var key = await AuthenticatorKey(_factory.Services, email);
        var enabled = await PostForm(browser, await browser.GetStringAsync("/Account/TwoFactor"), "/Account/EnableTwoFactor",
            new() { ["Code"] = Totp(key) });
        Assert.Contains("Save your recovery codes", await enabled.Content.ReadAsStringAsync());

        // A fresh browser: the password alone now leads to the code step, not into the account.
        var next = _factory.Browser();
        var login = await PostForm(next, await next.GetStringAsync("/Account/Login"), "/Account/Login",
            new() { ["Email"] = email, ["Password"] = PawConnectFactory.DemoPassword });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.StartsWith("/Account/LoginWith2fa", login.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, (await next.GetAsync("/Volunteer/Dashboard")).StatusCode); // still signed out

        var codePage = await next.GetStringAsync(login.Headers.Location!.OriginalString);
        var bad = await PostForm(next, codePage, "/Account/LoginWith2fa", new() { ["Code"] = "123456" });
        Assert.Equal(HttpStatusCode.OK, bad.StatusCode);
        Assert.Contains("didn&#x27;t work", await bad.Content.ReadAsStringAsync());

        var good = await PostForm(next, await next.GetStringAsync(login.Headers.Location!.OriginalString), "/Account/LoginWith2fa",
            new() { ["Code"] = Totp(key) });
        Assert.Equal(HttpStatusCode.Redirect, good.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await next.GetAsync("/Volunteer/Dashboard")).StatusCode);
    }

    [Fact]
    public async Task The_code_step_cannot_be_opened_without_a_correct_password_first()
    {
        var response = await _factory.Browser().GetAsync("/Account/LoginWith2fa");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Api_tokens_need_the_code_too()
    {
        const string email = "thabo@pawconnect.demo";
        var key = await TurnOnTwoFactor(_factory.Services, email);
        var client = _factory.Browser();

        var withoutCode = await client.PostAsJsonAsync("/api/auth/token", new { email, password = PawConnectFactory.DemoPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, withoutCode.StatusCode);
        Assert.Contains("code required", await withoutCode.Content.ReadAsStringAsync());

        var withCode = await client.PostAsJsonAsync("/api/auth/token",
            new { email, password = PawConnectFactory.DemoPassword, twoFactorCode = Totp(key) });
        Assert.Equal(HttpStatusCode.OK, withCode.StatusCode);
    }

    [Fact]
    public async Task When_required_admins_must_set_it_up_before_using_the_admin_area()
    {
        using var strict = _factory.WithWebHostBuilder(b => b.UseSetting("Security:RequireAdminTwoFactor", "true"));
        var admin = Browser(strict);
        await PawConnectFactory.LoginWithCookieAsync(admin, "admin@pawconnect.demo");

        var dashboard = await admin.GetAsync("/Admin/Dashboard");
        Assert.Equal(HttpStatusCode.Redirect, dashboard.StatusCode);
        Assert.StartsWith("/Account/TwoFactor", dashboard.Headers.Location!.OriginalString);
        Assert.Contains("Required for administrators", await admin.GetStringAsync("/Account/TwoFactor"));
    }

    [Fact]
    public async Task When_required_an_admin_with_it_on_gets_in()
    {
        using var strict = _factory.WithWebHostBuilder(b => b.UseSetting("Security:RequireAdminTwoFactor", "true"));
        const string email = "second.admin@test.local";
        using (var scope = strict.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(), UserName = email, Email = email, FirstName = "Second", LastName = "Admin",
                IsActive = true, CreatedAt = DateTime.UtcNow, ConsentAcceptedAt = DateTime.UtcNow
            };
            Assert.True((await users.CreateAsync(user, PawConnectFactory.DemoPassword)).Succeeded);
            await users.AddToRoleAsync(user, Roles.Administrator);
        }
        var key = await TurnOnTwoFactor(strict.Services, email);

        var admin = Browser(strict);
        var login = await PostForm(admin, await admin.GetStringAsync("/Account/Login"), "/Account/Login",
            new() { ["Email"] = email, ["Password"] = PawConnectFactory.DemoPassword });
        var codePage = await admin.GetStringAsync(login.Headers.Location!.OriginalString);
        var verified = await PostForm(admin, codePage, "/Account/LoginWith2fa", new() { ["Code"] = Totp(key) });
        Assert.Equal("/Admin/Dashboard", verified.Headers.Location!.OriginalString);

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/Admin/Dashboard")).StatusCode);
        // Required accounts can't switch it off.
        var settings = await admin.GetStringAsync("/Account/TwoFactor");
        Assert.Contains("must keep two-step verification on", settings);
    }

    // ---- helpers ----

    private static HttpClient Browser(WebApplicationFactory<Program> factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true
    });

    private static async Task<HttpResponseMessage> PostForm(HttpClient client, string pageHtml, string url, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = PawConnectFactory.ExtractFormToken(pageHtml);
        return await client.PostAsync(url, new FormUrlEncodedContent(fields));
    }

    private static async Task<string> AuthenticatorKey(IServiceProvider services, string email)
    {
        using var scope = services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return (await users.GetAuthenticatorKeyAsync((await users.FindByEmailAsync(email))!))!;
    }

    private static async Task<string> TurnOnTwoFactor(IServiceProvider services, string email)
    {
        using var scope = services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync(email))!;
        await users.ResetAuthenticatorKeyAsync(user);
        await users.SetTwoFactorEnabledAsync(user, true);
        return (await users.GetAuthenticatorKeyAsync(user))!;
    }

    /// <summary>The current 6-digit code an authenticator app would show (RFC 6238: HMAC-SHA1, 30 s steps).</summary>
    private static string Totp(string base32Key)
    {
        var key = Base32Decode(base32Key);
        var timestep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var counter = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(timestep));
        var hash = HMACSHA1.HashData(key, counter);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6");
    }

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = 0; var value = 0; var output = new List<byte>();
        foreach (var c in input.TrimEnd('=').ToUpperInvariant())
        {
            value = (value << 5) | alphabet.IndexOf(c);
            bits += 5;
            if (bits >= 8) { output.Add((byte)(value >> (bits - 8))); bits -= 8; }
        }
        return output.ToArray();
    }
}
