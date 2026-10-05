using Microsoft.EntityFrameworkCore;
using PawConnect.Core.Enums;
using PawConnect.Tests.Support;

namespace PawConnect.Tests.Services;

public class AdoptionServiceTests : IDisposable
{
    private readonly TestDb _t = new();
    public void Dispose() => _t.Dispose();

    [Fact]
    public async Task Submitting_creates_application_marks_animal_pending_and_emails_the_applicant()
    {
        var result = await _t.AdoptionService().SubmitAsync(_t.Adopter.Id, TestDb.ValidSubmission(_t.Dog.Id));

        Assert.True(result.Succeeded, result.Error);
        var app = await _t.Db.AdoptionApplications.Include(a => a.StatusHistory).SingleAsync();
        Assert.Equal(ApplicationStatus.Submitted, app.Status);
        Assert.StartsWith("PC-", app.ReferenceNumber);
        Assert.Single(app.StatusHistory);
        Assert.Equal(AnimalStatus.Pending, (await _t.Db.Animals.FindAsync(_t.Dog.Id))!.Status);
        var email = Assert.Single(_t.Notifications.Sent);
        Assert.Contains(app.ReferenceNumber, email.Subject);
        Assert.Equal("jordan@test.local", email.Email);
    }

    [Fact]
    public async Task A_pending_animal_cannot_receive_a_second_application()
    {
        var service = _t.AdoptionService();
        await service.SubmitAsync(_t.Adopter.Id, TestDb.ValidSubmission(_t.Dog.Id));
        var second = await service.SubmitAsync(_t.OtherAdopter.Id, TestDb.ValidSubmission(_t.Dog.Id));
        Assert.False(second.Succeeded);
        Assert.Contains("no longer available", second.Error);
    }

    [Theory]
    [InlineData("", "0791234567", "a@b.c", "Why")]
    [InlineData("Jordan", "", "a@b.c", "Why")]
    [InlineData("Jordan", "079", "not-an-email", "Why")]
    [InlineData("Jordan", "079", "a@b.c", "")]
    public async Task Incomplete_applications_are_rejected(string name, string phone, string email, string why)
    {
        var input = TestDb.ValidSubmission(_t.Dog.Id) with { FullName = name, Phone = phone, Email = email, Why = why };
        var result = await _t.AdoptionService().SubmitAsync(_t.Adopter.Id, input);
        Assert.False(result.Succeeded);
        Assert.Equal(0, await _t.Db.AdoptionApplications.CountAsync());
    }

    [Fact]
    public async Task Approval_is_only_allowed_after_review_starts()
    {
        var service = _t.AdoptionService();
        var app = (await service.SubmitAsync(_t.Adopter.Id, TestDb.ValidSubmission(_t.Dog.Id))).Value!;

        var tooEarly = await service.ApproveAsync(app.Id, _t.Volunteer.Id, null);
        Assert.False(tooEarly.Succeeded);

        Assert.True((await service.StartReviewAsync(app.Id, _t.Volunteer.Id)).Succeeded);
        Assert.True((await service.ApproveAsync(app.Id, _t.Volunteer.Id, "Great fit")).Succeeded);

        var saved = await _t.Db.AdoptionApplications.Include(a => a.StatusHistory).SingleAsync();
        Assert.Equal(ApplicationStatus.Approved, saved.Status);
        Assert.Equal(_t.Volunteer.Id, saved.ReviewedById);
        Assert.NotNull(saved.DecidedAt);
        Assert.Equal(3, saved.StatusHistory.Count);
        Assert.Contains(_t.Notifications.Sent, n => n.Subject.Contains("approved"));
    }

    [Fact]
    public async Task Completing_an_adoption_requires_home_visit_and_fee_then_marks_animal_adopted()
    {
        var adoptions = _t.AdoptionService();
        var donations = _t.DonationService();
        var app = (await adoptions.SubmitAsync(_t.Adopter.Id, TestDb.ValidSubmission(_t.Dog.Id))).Value!;
        await adoptions.StartReviewAsync(app.Id, _t.Volunteer.Id);
        await adoptions.ApproveAsync(app.Id, _t.Volunteer.Id, null);

        var noVisit = await adoptions.CompleteAdoptionAsync(app.Id, _t.Admin.Id);
        Assert.Contains("home visit", noVisit.Error);

        Assert.True((await adoptions.RecordHomeVisitAsync(app.Id, _t.Volunteer.Id, "Secure garden")).Succeeded);
        var noFee = await adoptions.CompleteAdoptionAsync(app.Id, _t.Admin.Id);
        Assert.Contains("adoption fee", noFee.Error);

        Assert.True((await donations.RecordAdoptionFeeAsync(app.Id, 950m, _t.Admin.Id)).Succeeded);
        Assert.True((await adoptions.CompleteAdoptionAsync(app.Id, _t.Admin.Id)).Succeeded);

        Assert.Equal(ApplicationStatus.Adopted, (await _t.Db.AdoptionApplications.SingleAsync()).Status);
        Assert.Equal(AnimalStatus.Adopted, (await _t.Db.Animals.FindAsync(_t.Dog.Id))!.Status);
    }

    [Fact]
    public async Task Rejecting_returns_the_animal_to_available_and_cannot_be_undone()
    {
        var service = _t.AdoptionService();
        var app = (await service.SubmitAsync(_t.Adopter.Id, TestDb.ValidSubmission(_t.Dog.Id))).Value!;
        Assert.True((await service.RejectAsync(app.Id, _t.Volunteer.Id, "Needs a secure garden")).Succeeded);

        Assert.Equal(AnimalStatus.Available, (await _t.Db.Animals.FindAsync(_t.Dog.Id))!.Status);
        Assert.False((await service.StartReviewAsync(app.Id, _t.Volunteer.Id)).Succeeded);
        Assert.False((await service.CompleteAdoptionAsync(app.Id, _t.Admin.Id)).Succeeded);
        Assert.Contains(_t.Notifications.Sent, n => n.Body.Contains("Needs a secure garden"));
    }

    [Fact]
    public async Task Only_the_owner_can_withdraw_an_application()
    {
        var service = _t.AdoptionService();
        var app = (await service.SubmitAsync(_t.Adopter.Id, TestDb.ValidSubmission(_t.Dog.Id))).Value!;

        Assert.False((await service.WithdrawAsync(app.Id, _t.OtherAdopter.Id)).Succeeded);
        Assert.True((await service.WithdrawAsync(app.Id, _t.Adopter.Id)).Succeeded);
        Assert.Equal(AnimalStatus.Available, (await _t.Db.Animals.FindAsync(_t.Dog.Id))!.Status);
    }

    [Fact]
    public async Task Home_visit_can_only_be_recorded_for_approved_applications()
    {
        var service = _t.AdoptionService();
        var app = (await service.SubmitAsync(_t.Adopter.Id, TestDb.ValidSubmission(_t.Dog.Id))).Value!;
        Assert.False((await service.RecordHomeVisitAsync(app.Id, _t.Volunteer.Id, null)).Succeeded);
    }
}
