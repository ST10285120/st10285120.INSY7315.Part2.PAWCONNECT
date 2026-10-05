using Microsoft.EntityFrameworkCore;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Services;
using PawConnect.Tests.Support;

namespace PawConnect.Tests.Services;

public class HourLogServiceTests : IDisposable
{
    private readonly TestDb _t = new();
    public void Dispose() => _t.Dispose();
    private static readonly DateOnly Today = new(2026, 9, 26);

    [Fact]
    public async Task Logs_hours_as_pending()
    {
        var result = await _t.HourLogService().LogAsync(_t.Volunteer.Id, new HourLogInput(Today, 2.5m, "Dog walking", "Walked Biscuit"));
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(HourLogStatus.Pending, (await _t.Db.HourLogs.SingleAsync()).Status);
    }

    [Theory]
    [InlineData(0, 0, "more than 0")]
    [InlineData(25, 0, "at most 24")]
    [InlineData(2.1, 0, "quarter-hour")]
    [InlineData(2, -1, "future")]
    [InlineData(2, 120, "older than")]
    public async Task Invalid_logs_are_rejected(double hours, int daysAgo, string message)
    {
        var result = await _t.HourLogService().LogAsync(_t.Volunteer.Id, new HourLogInput(Today.AddDays(-daysAgo), (decimal)hours, "Dog walking", null));
        Assert.False(result.Succeeded);
        Assert.Contains(message, result.Error);
    }

    [Fact]
    public async Task A_day_cannot_total_more_than_24_hours()
    {
        var service = _t.HourLogService();
        await service.LogAsync(_t.Volunteer.Id, new HourLogInput(Today, 20, "Events & fundraising", null));
        var result = await service.LogAsync(_t.Volunteer.Id, new HourLogInput(Today, 5, "Dog walking", null));
        Assert.False(result.Succeeded);
        Assert.Contains("can't exceed 24h", result.Error);
    }

    [Fact]
    public async Task Admin_review_counts_once_and_only_approved_hours_are_summed()
    {
        var service = _t.HourLogService();
        var a = (await service.LogAsync(_t.Volunteer.Id, new HourLogInput(Today, 3, "Dog walking", null))).Value!;
        var b = (await service.LogAsync(_t.Volunteer.Id, new HourLogInput(Today.AddDays(-1), 4, "Dog walking", null))).Value!;

        Assert.True((await service.ReviewAsync(a.Id, true, _t.Admin.Id)).Succeeded);
        Assert.True((await service.ReviewAsync(b.Id, false, _t.Admin.Id)).Succeeded);
        Assert.False((await service.ReviewAsync(a.Id, false, _t.Admin.Id)).Succeeded);

        Assert.Equal(3m, await _t.Hours.SumApprovedAsync(_t.Volunteer.Id, null, null));
    }
}

public class MedicalRecordServiceTests : IDisposable
{
    private readonly TestDb _t = new();
    public void Dispose() => _t.Dispose();

    [Fact]
    public async Task Logging_a_vaccination_updates_the_public_vaccination_badge()
    {
        var input = new MedicalRecordInput(MedicalRecordType.Vaccination, "Rabies", "No reaction", "Dr K", new DateOnly(2026, 9, 25), new DateOnly(2027, 9, 25));
        var result = await _t.MedicalRecordService().AddAsync(_t.Cat.Id, _t.Volunteer.Id, input);

        Assert.True(result.Succeeded, result.Error);
        Assert.True((await _t.Db.Animals.FindAsync(_t.Cat.Id))!.IsVaccinated);
        var history = await _t.MedicalRecords.GetForAnimalAsync(_t.Cat.Id);
        Assert.Equal("Priya Naidoo", Assert.Single(history).RecordedBy!.FullName);
    }

    [Fact]
    public async Task Future_dates_and_backwards_due_dates_are_rejected()
    {
        var service = _t.MedicalRecordService();
        Assert.False((await service.AddAsync(_t.Cat.Id, _t.Volunteer.Id, new MedicalRecordInput(MedicalRecordType.Checkup, "Check", null, null, new DateOnly(2026, 10, 1), null))).Succeeded);
        Assert.False((await service.AddAsync(_t.Cat.Id, _t.Volunteer.Id, new MedicalRecordInput(MedicalRecordType.Checkup, "Check", null, null, new DateOnly(2026, 9, 1), new DateOnly(2026, 8, 1)))).Succeeded);
        Assert.False((await service.AddAsync(Guid.NewGuid(), _t.Volunteer.Id, new MedicalRecordInput(MedicalRecordType.Checkup, "Check", null, null, new DateOnly(2026, 9, 1), null))).Succeeded);
    }
}

public class DonationServiceTests : IDisposable
{
    private readonly TestDb _t = new();
    public void Dispose() => _t.Dispose();

    [Fact]
    public async Task General_pledge_gets_receipt_number_and_receipt_email()
    {
        var result = await _t.DonationService().PledgeAsync(_t.Adopter.Id, new DonationPledge(250m, DonationFrequency.OnceOff, null, null));
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(DonationType.General, result.Value!.Type);
        Assert.Equal(DonationStatus.Pledged, result.Value.Status);
        Assert.StartsWith("RCPT-", result.Value.ReceiptNumber);
        Assert.Contains(_t.Notifications.Sent, n => n.Subject.Contains(result.Value.ReceiptNumber));
    }

    [Fact]
    public async Task Sponsoring_an_animal_links_the_donation()
    {
        var result = await _t.DonationService().PledgeAsync(_t.Adopter.Id, new DonationPledge(500m, DonationFrequency.Monthly, _t.Dog.Id, null));
        Assert.Equal(DonationType.Sponsorship, result.Value!.Type);
        Assert.Equal(_t.Dog.Id, result.Value.AnimalId);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(100001)]
    [InlineData(10.555)]
    public async Task Out_of_range_amounts_are_rejected(double amount) =>
        Assert.False((await _t.DonationService().PledgeAsync(_t.Adopter.Id, new DonationPledge((decimal)amount, DonationFrequency.OnceOff, null, null))).Succeeded);

    [Fact]
    public async Task Adopted_animals_cannot_be_sponsored()
    {
        var adopted = _t.NewAnimal("Rocky", Species.Dog, 56, AnimalStatus.Adopted);
        _t.Db.Animals.Add(adopted);
        await _t.Db.SaveChangesAsync();
        Assert.False((await _t.DonationService().PledgeAsync(_t.Adopter.Id, new DonationPledge(100m, DonationFrequency.OnceOff, adopted.Id, null))).Succeeded);
    }

    [Fact]
    public async Task Receipts_are_only_visible_to_the_donor_or_staff()
    {
        var service = _t.DonationService();
        var d = (await service.PledgeAsync(_t.Adopter.Id, new DonationPledge(100m, DonationFrequency.OnceOff, null, null))).Value!;
        Assert.NotNull(await service.GetReceiptAsync(d.Id, _t.Adopter.Id, requesterIsStaff: false));
        Assert.Null(await service.GetReceiptAsync(d.Id, _t.OtherAdopter.Id, requesterIsStaff: false));
        Assert.NotNull(await service.GetReceiptAsync(d.Id, _t.Admin.Id, requesterIsStaff: true));
    }

    [Fact]
    public async Task Marking_received_only_works_once()
    {
        var service = _t.DonationService();
        var d = (await service.PledgeAsync(_t.Adopter.Id, new DonationPledge(100m, DonationFrequency.OnceOff, null, null))).Value!;
        Assert.True((await service.MarkReceivedAsync(d.Id, _t.Admin.Id)).Succeeded);
        Assert.False((await service.MarkReceivedAsync(d.Id, _t.Admin.Id)).Succeeded);
        Assert.False((await service.CancelAsync(d.Id, _t.Admin.Id)).Succeeded);
    }

    [Fact]
    public async Task Adoption_fee_requires_an_approved_application()
    {
        var app = (await _t.AdoptionService().SubmitAsync(_t.Adopter.Id, TestDb.ValidSubmission(_t.Dog.Id))).Value!;
        Assert.False((await _t.DonationService().RecordAdoptionFeeAsync(app.Id, 950m, _t.Admin.Id)).Succeeded);
    }
}

public class AnimalServiceTests : IDisposable
{
    private readonly TestDb _t = new();
    public void Dispose() => _t.Dispose();
    private static AnimalInput Input(string name = "Zola", int kennel = 300) =>
        new(name, Species.Dog, "Collie", 8, AnimalSize.Medium, true, true, false, kennel, "Clever girl", null, null);

    [Fact]
    public async Task New_animals_start_available_in_the_default_branch()
    {
        var result = await _t.AnimalService().CreateAsync(Input());
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(AnimalStatus.Available, result.Value!.Status);
        Assert.Equal(_t.Branch.Id, result.Value.BranchId);
        Assert.Equal(new DateOnly(2026, 9, 26), result.Value.IntakeDate);
    }

    [Fact]
    public async Task Kennel_numbers_cannot_be_shared_by_two_animals_in_care()
    {
        var result = await _t.AnimalService().CreateAsync(Input(kennel: 214)); // Biscuit's kennel
        Assert.False(result.Succeeded);
        Assert.Contains("Kennel 214", result.Error);
    }

    [Fact]
    public async Task Cannot_mark_available_while_an_application_is_open()
    {
        await _t.AdoptionService().SubmitAsync(_t.Adopter.Id, TestDb.ValidSubmission(_t.Dog.Id));
        var result = await _t.AnimalService().SetStatusAsync(_t.Dog.Id, AnimalStatus.Available);
        Assert.False(result.Succeeded);
        Assert.Contains("open adoption application", result.Error);
    }

    [Fact]
    public async Task Photos_are_validated_and_the_first_becomes_primary()
    {
        var service = _t.AnimalService();
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3 };
        Assert.False((await service.AddPhotoAsync(_t.Dog.Id, "<script>"u8.ToArray())).Succeeded);
        Assert.False((await service.AddPhotoAsync(_t.Dog.Id, new byte[AnimalService.MaxPhotoBytes + 1])).Succeeded);

        var first = await service.AddPhotoAsync(_t.Dog.Id, png);
        var second = await service.AddPhotoAsync(_t.Dog.Id, png);
        Assert.True(first.Value!.IsPrimary);
        Assert.False(second.Value!.IsPrimary);

        Assert.True((await service.DeletePhotoAsync(_t.Dog.Id, first.Value.Id)).Succeeded);
        Assert.True(Assert.Single(await _t.Animals.GetPhotoInfosAsync(_t.Dog.Id)).IsPrimary);
    }
}

public class ReportServiceTests : IDisposable
{
    private readonly TestDb _t = new();
    public void Dispose() => _t.Dispose();

    private void AddDonation(decimal amount, DateTime at, DonationStatus status = DonationStatus.Received, DonationType type = DonationType.General) =>
        _t.Db.Donations.Add(new Donation
        {
            Id = Guid.NewGuid(), DonorId = _t.Adopter.Id, Amount = amount, Type = type, Status = status, DonatedAt = at,
            ReceivedAt = status == DonationStatus.Received ? at : null, ReceiptNumber = "R-" + Guid.NewGuid().ToString("N")[..8]
        });

    [Fact]
    public async Task Monthly_totals_group_received_donations_by_shelter_month()
    {
        AddDonation(100, new DateTime(2026, 9, 2, 8, 0, 0, DateTimeKind.Utc));
        AddDonation(250, new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc));
        AddDonation(400, new DateTime(2026, 8, 31, 23, 0, 0, DateTimeKind.Utc)); // 1 Sept 01:00 in SAST
        AddDonation(999, new DateTime(2026, 7, 10, 8, 0, 0, DateTimeKind.Utc), DonationStatus.Pledged); // not received
        await _t.Db.SaveChangesAsync();

        var months = await _t.ReportService().GetMonthlyDonationsAsync(3);
        Assert.Equal(new[] { "Jul", "Aug", "Sep" }, months.Select(m => m.Label).ToArray());
        Assert.Equal(new[] { 0m, 0m, 750m }, months.Select(m => m.Amount).ToArray());
    }

    [Fact]
    public async Task Summary_splits_donations_by_type_and_counts_pledges_separately()
    {
        AddDonation(100, new DateTime(2026, 9, 2, 8, 0, 0, DateTimeKind.Utc));
        AddDonation(300, new DateTime(2026, 9, 3, 8, 0, 0, DateTimeKind.Utc), type: DonationType.Sponsorship);
        AddDonation(950, new DateTime(2026, 9, 4, 8, 0, 0, DateTimeKind.Utc), type: DonationType.AdoptionFee);
        AddDonation(50, new DateTime(2026, 9, 5, 8, 0, 0, DateTimeKind.Utc), DonationStatus.Pledged);
        await _t.Db.SaveChangesAsync();

        var s = (await _t.ReportService().GetSummaryAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30))).Value!;
        Assert.Equal(1350m, s.DonationsReceived);
        Assert.Equal(3, s.DonationCount);
        Assert.Equal(300m, s.Sponsorships);
        Assert.Equal(950m, s.AdoptionFees);
        Assert.Equal(50m, s.OutstandingPledges);
        Assert.Equal(4, s.Donations.Count);

        var csv = ReportService.ToCsv(s.Donations);
        Assert.Equal(5, csv.Trim().Split('\n').Length); // header + 4 rows
    }

    [Fact]
    public async Task Summary_rejects_backwards_ranges() =>
        Assert.False((await _t.ReportService().GetSummaryAsync(new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 1))).Succeeded);

    [Fact]
    public async Task Dashboard_counts_animals_in_care()
    {
        var stats = await _t.ReportService().GetDashboardAsync();
        Assert.Equal(2, stats.AnimalsInCare);
        Assert.Equal(0, stats.OpenApplications);
    }
}
