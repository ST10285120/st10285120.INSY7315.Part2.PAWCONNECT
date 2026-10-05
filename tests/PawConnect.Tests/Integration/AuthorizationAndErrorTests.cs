using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Infrastructure.Data;

namespace PawConnect.Tests.Integration;

/// <summary>
/// Broken access control (OWASP A01) and API error contracts: nobody can reach another person's
/// data by changing an id, volunteers are limited to their branch, and every error comes back
/// as RFC 7807 problem details with the right status code.
/// </summary>
public class AuthorizationAndErrorTests : IClassFixture<PawConnectFactory>
{
    private readonly PawConnectFactory _factory;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public AuthorizationAndErrorTests(PawConnectFactory factory) => _factory = factory;

    private record AnimalDto(Guid Id, string Name, string Status);
    private record IdDto(Guid Id);

    private async Task<Guid> SubmitApplicationFor(HttpClient adopter, string animalName)
    {
        var animal = (await adopter.GetFromJsonAsync<List<AnimalDto>>("/api/animals?search=" + animalName, Json))!.Single();
        var submit = await adopter.PostAsJsonAsync("/api/applications", new
        {
            animalId = animal.Id, fullName = "Jordan Adams", phone = "0791234567", email = "adopter@pawconnect.demo",
            why = "Lots of love to give.", homeType = "House with garden", ownRent = "Own", otherPets = "None"
        });
        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);
        return (await submit.Content.ReadFromJsonAsync<IdDto>(Json))!.Id;
    }

    /// <summary>Creates a user directly (another adopter, or a volunteer at a second branch).</summary>
    private async Task<string> CreateUser(string role, bool atNewBranch = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Guid? branchId = null;
        if (atNewBranch)
        {
            var branch = new ShelterBranch { Id = Guid.NewGuid(), Name = "Branch " + Guid.NewGuid().ToString("N")[..6], Location = "Elsewhere" };
            db.Branches.Add(branch);
            await db.SaveChangesAsync();
            branchId = branch.Id;
        }
        var email = $"{role.ToLower()}-{Guid.NewGuid():N}@test.local";
        var user = new ApplicationUser { UserName = email, Email = email, FirstName = "Test", LastName = role, BranchId = branchId, IsActive = true, EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user, PawConnectFactory.DemoPassword)).Succeeded);
        await users.AddToRoleAsync(user, role);
        return email;
    }

    [Fact]
    public async Task An_adopter_cannot_read_or_withdraw_someone_elses_application()
    {
        var owner = await _factory.ApiClientFor("adopter@pawconnect.demo");
        var id = await SubmitApplicationFor(owner, "Luna");
        var stranger = await _factory.ApiClientFor(await CreateUser(Roles.Adopter));

        var read = await stranger.GetAsync($"/api/applications/{id}");
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode); // 404, not 403: ids can't be probed
        Assert.Equal("application/problem+json", read.Content.Headers.ContentType!.MediaType);

        var withdraw = await stranger.PostAsync($"/api/applications/{id}/withdraw", null);
        Assert.Equal(HttpStatusCode.NotFound, withdraw.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"/api/applications/{id}/withdraw", null)).StatusCode);
    }

    [Fact]
    public async Task A_volunteer_from_another_branch_cannot_see_or_decide_an_application()
    {
        var adopter = await _factory.ApiClientFor("adopter@pawconnect.demo");
        var id = await SubmitApplicationFor(adopter, "Whiskers");
        var outsider = await _factory.ApiClientFor(await CreateUser(Roles.Volunteer, atNewBranch: true));

        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/applications/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.PostAsJsonAsync($"/api/applications/{id}/transitions", new { status = "UnderReview" })).StatusCode);

        // The page is closed to them too.
        var browser = _factory.Browser();
        await PawConnectFactory.LoginWithCookieAsync(browser, await CreateUser(Roles.Volunteer, atNewBranch: true));
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/Volunteer/Application/{id}")).StatusCode);

        // The shelter's own volunteer can work on it.
        var priya = await _factory.ApiClientFor("priya@pawconnect.demo");
        Assert.Equal(HttpStatusCode.NoContent, (await priya.PostAsJsonAsync($"/api/applications/{id}/transitions", new { status = "UnderReview" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await adopter.PostAsync($"/api/applications/{id}/withdraw", null)).StatusCode);
    }

    [Fact]
    public async Task A_donor_cannot_read_another_donors_donation()
    {
        var donor = await _factory.ApiClientFor("adopter@pawconnect.demo");
        var created = await donor.PostAsJsonAsync("/api/donations", new { amount = 150, frequency = "OnceOff" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Contains("/api/donations/", created.Headers.Location!.ToString()); // Location points at the new resource
        var id = (await created.Content.ReadFromJsonAsync<IdDto>(Json))!.Id;

        Assert.Equal(HttpStatusCode.OK, (await donor.GetAsync($"/api/donations/{id}")).StatusCode);
        var stranger = await _factory.ApiClientFor(await CreateUser(Roles.Adopter));
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/donations/{id}")).StatusCode);
    }

    [Fact]
    public async Task Status_codes_follow_http_semantics()
    {
        var volunteer = await _factory.ApiClientFor("priya@pawconnect.demo");
        var admin = await _factory.ApiClientFor("admin@pawconnect.demo");

        // 404: the application doesn't exist.
        var missing = await volunteer.PostAsJsonAsync($"/api/applications/{Guid.NewGuid()}/transitions", new { status = "UnderReview" });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        // 400: a request that can never be valid (staff can't move an application back to Submitted).
        var adopter = await _factory.ApiClientFor("adopter@pawconnect.demo");
        var id = await SubmitApplicationFor(adopter, "Nala");
        var invalid = await volunteer.PostAsJsonAsync($"/api/applications/{id}/transitions", new { status = "Submitted" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        // 409: valid request, but it clashes with the current state (must be reviewed before approval).
        var conflict = await volunteer.PostAsJsonAsync($"/api/applications/{id}/transitions", new { status = "Approved" });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("application/problem+json", conflict.Content.Headers.ContentType!.MediaType);
        await adopter.PostAsync($"/api/applications/{id}/withdraw", null);

        // 404 for an unknown animal on the admin status endpoint.
        var unknownAnimal = await admin.PatchAsJsonAsync($"/api/animals/{Guid.NewGuid()}/status", "Fostered");
        Assert.Equal(HttpStatusCode.NotFound, unknownAnimal.StatusCode);

        // 400: enums must be sent by name; a number could store an undefined value.
        var anyAnimal = (await admin.GetFromJsonAsync<List<AnimalDto>>("/api/animals", Json))!.First();
        var numeric = await admin.PatchAsync($"/api/animals/{anyAnimal.Id}/status",
            new StringContent("99", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, numeric.StatusCode);

        // 400: report dates are required.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/reports/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/reports/summary?from=2026-09-01&to=2026-09-30")).StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_api_calls_get_a_problem_details_401()
    {
        var response = await _factory.Browser().GetAsync("/api/applications/mine");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task A_deactivated_account_is_only_revealed_to_someone_who_knows_the_password()
    {
        var email = await CreateUser(Roles.Adopter);
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByEmailAsync(email);
            user!.IsActive = false;
            await users.UpdateAsync(user);
        }
        var client = _factory.Browser();

        var wrong = await client.PostAsJsonAsync("/api/auth/token", new { email, password = "Wrong!Pass1" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Contains("Incorrect email or password", await wrong.Content.ReadAsStringAsync());

        var right = await client.PostAsJsonAsync("/api/auth/token", new { email, password = PawConnectFactory.DemoPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, right.StatusCode);
        Assert.Contains("deactivated", await right.Content.ReadAsStringAsync());
    }
}
