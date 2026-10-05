using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PawConnect.Tests.Integration;

public class ApiTests : IClassFixture<PawConnectFactory>
{
    private readonly PawConnectFactory _factory;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public ApiTests(PawConnectFactory factory) => _factory = factory;

    private record AnimalDto(Guid Id, string Name, string Species, string Status, bool IsVaccinated);
    private record MedicalDto(Guid Id, string Title, string RecordType);
    private record DetailDto(AnimalDto Animal, List<MedicalDto>? MedicalHistory);
    private record ApplicationDto(Guid Id, string ReferenceNumber, string Status);
    private record ShiftDto(Guid Id, string Role, int Capacity, int Filled, bool IsFull, bool SignedUp);

    private async Task<List<AnimalDto>> Animals(HttpClient c, string query = "") =>
        (await c.GetFromJsonAsync<List<AnimalDto>>("/api/animals" + query, Json))!;

    [Fact]
    public async Task Home_page_and_health_check_respond()
    {
        var client = _factory.Browser();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
        Assert.Equal("Healthy", await client.GetStringAsync("/health"));
        Assert.Contains("\"environment\":\"Testing\"", await client.GetStringAsync("/version"));
    }

    private record PhotoCardDto(string Name, string? PhotoUrl);

    [Fact]
    public async Task Every_demo_animal_has_a_real_photo_that_is_served_as_jpeg()
    {
        var client = _factory.Browser();
        var animals = (await client.GetFromJsonAsync<List<PhotoCardDto>>("/api/animals", Json))!;
        Assert.Equal(9, animals.Count);
        Assert.All(animals, a => Assert.False(string.IsNullOrEmpty(a.PhotoUrl), $"{a.Name} has no photo"));

        var photo = await client.GetAsync(animals[0].PhotoUrl);
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal("image/jpeg", photo.Content.Headers.ContentType!.MediaType);
        var bytes = await photo.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 10_000);
        Assert.Equal(new byte[] { 0xFF, 0xD8 }, bytes[..2]); // JPEG signature
    }

    [Fact]
    public async Task Responses_carry_security_headers()
    {
        var response = await _factory.Browser().GetAsync("/");
        Assert.Contains("script-src 'self'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task Public_can_list_and_filter_animals()
    {
        var client = _factory.Browser();
        var all = await Animals(client);
        var cats = await Animals(client, "?species=Cat&available=true");
        Assert.True(all.Count >= 8);
        Assert.NotEmpty(cats);
        Assert.All(cats, c => { Assert.Equal("Cat", c.Species); Assert.Equal("Available", c.Status); });
    }

    [Fact]
    public async Task Medical_history_is_staff_only()
    {
        var anonymous = _factory.Browser();
        var id = (await Animals(anonymous)).First(a => a.Name == "Biscuit").Id;
        var url = $"/api/animals/{id}?include=history";

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(url)).StatusCode);
        var adopter = await _factory.ApiClientFor("adopter@pawconnect.demo");
        Assert.Equal(HttpStatusCode.Forbidden, (await adopter.GetAsync(url)).StatusCode);

        var volunteer = await _factory.ApiClientFor("priya@pawconnect.demo");
        var detail = await volunteer.GetFromJsonAsync<DetailDto>(url, Json);
        Assert.NotEmpty(detail!.MedicalHistory!);

        // Without ?include=history the public profile is still available to everyone.
        Assert.Null((await anonymous.GetFromJsonAsync<DetailDto>($"/api/animals/{id}", Json))!.MedicalHistory);
    }

    [Fact]
    public async Task Wrong_password_is_refused()
    {
        var response = await _factory.Browser().PostAsJsonAsync("/api/auth/token", new { email = "priya@pawconnect.demo", password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Volunteers_can_log_medical_records_but_adopters_cannot()
    {
        var volunteer = await _factory.ApiClientFor("priya@pawconnect.demo");
        var milo = (await Animals(volunteer)).First(a => a.Name == "Milo");
        var body = new { animalId = milo.Id, recordType = "Vaccination", title = "Second kitten vaccine", recordDate = "2026-09-01" };

        var adopter = await _factory.ApiClientFor("adopter@pawconnect.demo");
        Assert.Equal(HttpStatusCode.Forbidden, (await adopter.PostAsJsonAsync("/api/medical-records", body)).StatusCode);

        var created = await volunteer.PostAsJsonAsync("/api/medical-records", body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var after = (await Animals(volunteer)).First(a => a.Name == "Milo");
        Assert.True(after.IsVaccinated); // vaccination badge updated straight away
    }

    [Fact]
    public async Task Invalid_payloads_get_a_400_problem_response()
    {
        var volunteer = await _factory.ApiClientFor("priya@pawconnect.demo");
        var response = await volunteer.PostAsJsonAsync("/api/medical-records", new { animalId = Guid.NewGuid(), recordType = "Checkup", title = "", recordDate = "2026-09-01" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Full_adoption_workflow_through_the_api()
    {
        var adopter = await _factory.ApiClientFor("adopter@pawconnect.demo");
        var volunteer = await _factory.ApiClientFor("priya@pawconnect.demo");
        var pumpkin = (await Animals(adopter, "?search=Pumpkin")).Single();

        var submit = await adopter.PostAsJsonAsync("/api/applications", new
        {
            animalId = pumpkin.Id, fullName = "Jordan Adams", phone = "0791234567", email = "adopter@pawconnect.demo",
            why = "Quiet home, lots of sunny windows.", homeType = "Apartment or flat", ownRent = "Rent", otherPets = "None"
        });
        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);
        var app = (await submit.Content.ReadFromJsonAsync<ApplicationDto>(Json))!;

        // Pumpkin is now pending, so a second application is refused.
        Assert.Equal("Pending", (await Animals(adopter, "?search=Pumpkin")).Single().Status);

        // Adopters can't move their own application through review.
        Assert.Equal(HttpStatusCode.Forbidden, (await adopter.PostAsJsonAsync($"/api/applications/{app.Id}/transitions", new { status = "Approved" })).StatusCode);

        // The state machine refuses skipping review.
        Assert.Equal(HttpStatusCode.Conflict, (await volunteer.PostAsJsonAsync($"/api/applications/{app.Id}/transitions", new { status = "Approved" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await volunteer.PostAsJsonAsync($"/api/applications/{app.Id}/transitions", new { status = "UnderReview" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await volunteer.PostAsJsonAsync($"/api/applications/{app.Id}/transitions", new { status = "Approved", note = "Welcome!" })).StatusCode);

        var mine = await adopter.GetFromJsonAsync<List<ApplicationDto>>("/api/applications/mine", Json);
        Assert.Equal("Approved", mine!.Single(a => a.Id == app.Id).Status);

        // Another adopter can't read it (ownership check), staff can.
        var other = _factory.Browser();
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync($"/api/applications/{app.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await volunteer.GetAsync($"/api/applications/{app.Id}")).StatusCode);
    }

    [Fact]
    public async Task Shift_sign_up_via_api_and_capacity_is_enforced()
    {
        var priya = await _factory.ApiClientFor("priya@pawconnect.demo");
        var shifts = (await priya.GetFromJsonAsync<List<ShiftDto>>("/api/shifts", Json))!;
        var full = shifts.First(s => s.IsFull);
        var open = shifts.First(s => !s.IsFull && !s.SignedUp && s.Role == "Adoption day event");

        Assert.Equal(HttpStatusCode.Conflict, (await priya.PostAsync($"/api/shifts/{full.Id}/signup", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await priya.PostAsync($"/api/shifts/{open.Id}/signup", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await priya.PostAsync($"/api/shifts/{open.Id}/signup", null)).StatusCode); // twice
        Assert.Equal(HttpStatusCode.NoContent, (await priya.DeleteAsync($"/api/shifts/{open.Id}/signup")).StatusCode);

        var adopter = await _factory.ApiClientFor("adopter@pawconnect.demo");
        Assert.Equal(HttpStatusCode.Forbidden, (await adopter.GetAsync("/api/shifts")).StatusCode);
    }

    [Fact]
    public async Task Cookie_authenticated_api_calls_need_the_csrf_header()
    {
        var browser = _factory.Browser();
        var csrf = await PawConnectFactory.LoginWithCookieAsync(browser, "thabo@pawconnect.demo");
        var shift = (await browser.GetFromJsonAsync<List<ShiftDto>>("/api/shifts", Json))!.First(s => !s.IsFull && !s.SignedUp && s.Role == "Adoption day event");

        var withoutToken = await browser.PostAsync($"/api/shifts/{shift.Id}/signup", null);
        Assert.Equal(HttpStatusCode.BadRequest, withoutToken.StatusCode);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/shifts/{shift.Id}/signup");
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        Assert.Equal(HttpStatusCode.NoContent, (await browser.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Admin_only_pages_and_reports_are_protected()
    {
        var anonymous = _factory.Browser();
        var redirect = await anonymous.GetAsync("/Admin/Dashboard");
        Assert.Equal(HttpStatusCode.Redirect, redirect.StatusCode);
        Assert.Contains("/Account/Login", redirect.Headers.Location!.ToString());

        var volunteer = await _factory.ApiClientFor("priya@pawconnect.demo");
        Assert.Equal(HttpStatusCode.Forbidden, (await volunteer.GetAsync("/api/reports/summary?from=2026-01-01&to=2026-12-31")).StatusCode);

        var admin = await _factory.ApiClientFor("admin@pawconnect.demo");
        var csv = await admin.GetAsync("/api/reports/donations.csv?from=2026-01-01&to=2026-12-31");
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.StartsWith("Receipt number,", await csv.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Html_pages_render_for_each_role()
    {
        foreach (var (email, paths) in new[]
        {
            ("adopter@pawconnect.demo", new[] { "/Home/Browse", "/Applications/Mine", "/Home/Donate", "/Account/Me" }),
            ("priya@pawconnect.demo", new[] { "/Volunteer/Dashboard" }),
            ("admin@pawconnect.demo", new[] { "/Admin/Dashboard", "/Admin/Animals", "/Admin/Volunteers", "/Admin/Hours", "/Admin/Users", "/Admin/Shifts", "/Admin/Donations", "/Admin/Reports" })
        })
        {
            var browser = _factory.Browser();
            await PawConnectFactory.LoginWithCookieAsync(browser, email);
            foreach (var path in paths)
                Assert.True((await browser.GetAsync(path)).StatusCode == HttpStatusCode.OK, $"{email} {path}");
        }
    }

    [Fact]
    public async Task Unknown_pages_return_a_friendly_404()
    {
        var response = await _factory.Browser().GetAsync("/no-such-page");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("find that page", await response.Content.ReadAsStringAsync());
    }
}
