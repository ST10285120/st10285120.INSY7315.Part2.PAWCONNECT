using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PawConnect.Tests.Integration;

/// <summary>Account stories: registration with POPIA consent, data export, lockout, admin user management.</summary>
public class AccountTests : IClassFixture<PawConnectFactory>
{
    private readonly PawConnectFactory _factory;
    public AccountTests(PawConnectFactory factory) => _factory = factory;

    private static async Task<HttpResponseMessage> PostForm(HttpClient client, string pageUrl, string postUrl, Dictionary<string, string> fields)
    {
        var page = await client.GetStringAsync(pageUrl);
        fields["__RequestVerificationToken"] = PawConnectFactory.ExtractFormToken(page);
        return await client.PostAsync(postUrl, new FormUrlEncodedContent(fields));
    }

    private static Dictionary<string, string> Registration(string email, bool consent)
    {
        var f = new Dictionary<string, string>
        {
            ["FirstName"] = "Test", ["LastName"] = "Person", ["Email"] = email, ["Phone"] = "0820000000",
            ["Password"] = "Adopt2026x", ["ConfirmPassword"] = "Adopt2026x"
        };
        if (consent) f["AcceptPrivacy"] = "true";
        return f;
    }

    [Fact]
    public async Task Registration_requires_popia_consent_and_creates_an_adopter()
    {
        var browser = _factory.Browser();
        var refused = await PostForm(browser, "/Account/Register", "/Account/Register", Registration("noconsent@test.local", consent: false));
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode); // form shown again with an error
        Assert.Contains("accept the privacy notice", await refused.Content.ReadAsStringAsync());

        var accepted = await PostForm(browser, "/Account/Register", "/Account/Register", Registration("consent@test.local", consent: true));
        Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);

        // The new account is an adopter: it may use adopter endpoints but not staff ones.
        var api = await TokenClient("consent@test.local", "Adopt2026x");
        Assert.Equal(HttpStatusCode.OK, (await api.GetAsync("/api/applications/mine")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.GetAsync("/api/shifts")).StatusCode);
    }

    [Fact]
    public async Task Users_can_download_all_their_data()
    {
        var browser = _factory.Browser();
        await PawConnectFactory.LoginWithCookieAsync(browser, "adopter@pawconnect.demo");
        var response = await browser.GetAsync("/Account/ExportMyData");

        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("adopter@pawconnect.demo", json.RootElement.GetProperty("account").GetProperty("Email").GetString());
        Assert.True(json.RootElement.GetProperty("applications").GetArrayLength() >= 1);
        Assert.True(json.RootElement.GetProperty("donations").GetArrayLength() >= 1);
    }

    [Fact]
    public async Task Account_locks_after_five_failed_sign_ins()
    {
        var client = _factory.Browser();
        for (var i = 0; i < 5; i++)
            await client.PostAsJsonAsync("/api/auth/token", new { email = "sipho@pawconnect.demo", password = "wrong-password" });

        // Even the correct password is refused while the account is locked.
        var locked = await client.PostAsJsonAsync("/api/auth/token", new { email = "sipho@pawconnect.demo", password = PawConnectFactory.DemoPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Contains("locked", await locked.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Admin_can_create_a_volunteer_and_deactivate_them()
    {
        var admin = _factory.Browser();
        await PawConnectFactory.LoginWithCookieAsync(admin, "admin@pawconnect.demo");

        var created = await PostForm(admin, "/Admin/Users", "/Admin/CreateUser", new Dictionary<string, string>
        {
            ["NewUser.FirstName"] = "Kagiso", ["NewUser.LastName"] = "Molefe", ["NewUser.Email"] = "kagiso@test.local",
            ["NewUser.Role"] = "Volunteer", ["NewUser.VolunteerRole"] = "Transport", ["NewUser.TemporaryPassword"] = "Temp2026pass"
        });
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);

        // The new volunteer can use volunteer endpoints straight away.
        var volunteer = await TokenClient("kagiso@test.local", "Temp2026pass");
        Assert.Equal(HttpStatusCode.OK, (await volunteer.GetAsync("/api/shifts")).StatusCode);

        // Find their id on the Users page, then deactivate them.
        var usersPage = await admin.GetStringAsync("/Admin/Users?filter=Volunteer");
        var row = usersPage[usersPage.IndexOf("kagiso@test.local", StringComparison.Ordinal)..];
        var userId = System.Text.RegularExpressions.Regex.Match(row, "name=\"userId\" value=\"([0-9a-f-]{36})\"").Groups[1].Value;
        var deactivated = await PostForm(admin, "/Admin/Users", "/Admin/SetActive",
            new Dictionary<string, string> { ["userId"] = userId, ["active"] = "false" });
        Assert.Equal(HttpStatusCode.Redirect, deactivated.StatusCode);

        var refused = await _factory.Browser().PostAsJsonAsync("/api/auth/token", new { email = "kagiso@test.local", password = "Temp2026pass" });
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
    }

    private async Task<HttpClient> TokenClient(string email, string password)
    {
        var client = _factory.Browser();
        var response = await client.PostAsJsonAsync("/api/auth/token", new { email, password });
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<PawConnectFactory.TokenResponse>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
