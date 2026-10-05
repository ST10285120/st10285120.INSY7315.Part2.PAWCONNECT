using Microsoft.EntityFrameworkCore;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Services;
using PawConnect.Tests.Support;

namespace PawConnect.Tests.Database;

/// <summary>
/// Data-integrity and concurrency rules proven against a REAL PostgreSQL database built by the
/// app's migrations. The in-memory provider can't enforce CHECK constraints, partial unique
/// indexes or SQL behaviour, so these tests are what shows the database itself protects the data.
/// Two DbContexts (TestDb + NewScope) play two people acting at the same moment.
/// </summary>
public class PostgresIntegrityTests
{
    // ---------- Schema ----------

    [PostgresFact]
    public void Migrations_build_the_full_schema_with_no_pending_model_changes()
    {
        using var pg = new PostgresTestDb();
        Assert.Empty(pg.T.Db.Database.GetPendingMigrations());
        Assert.Contains(pg.T.Db.Database.GetAppliedMigrations(), m => m.EndsWith("IntegrityConstraintsAndConcurrency"));
    }

    // ---------- CHECK constraints ----------

    [PostgresFact]
    public async Task Database_rejects_a_zero_donation_even_if_the_code_is_bypassed()
    {
        using var pg = new PostgresTestDb();
        var t = pg.T;
        t.Db.Donations.Add(new Donation
        {
            Id = Guid.NewGuid(), DonorId = t.Adopter.Id, Amount = 0m, Type = DonationType.General,
            Frequency = DonationFrequency.OnceOff, Status = DonationStatus.Pledged, DonatedAt = t.Clock.UtcNow, ReceiptNumber = "R-TEST-0"
        });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => t.Db.SaveChangesAsync());
        Assert.Contains("CK_Donations_Amount", ex.InnerException!.Message);
    }

    [PostgresFact]
    public async Task Database_rejects_more_than_24_hours_logged_in_one_entry()
    {
        using var pg = new PostgresTestDb();
        var t = pg.T;
        t.Db.HourLogs.Add(new HourLog
        {
            Id = Guid.NewGuid(), VolunteerId = t.Volunteer.Id, Date = new DateOnly(2026, 9, 25), Hours = 25m,
            Activity = "Dog walking", Status = HourLogStatus.Pending, LoggedAt = t.Clock.UtcNow
        });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => t.Db.SaveChangesAsync());
        Assert.Contains("CK_HourLogs_Hours", ex.InnerException!.Message);
    }

    [PostgresFact]
    public async Task Database_rejects_a_shift_that_ends_before_it_starts()
    {
        using var pg = new PostgresTestDb();
        var t = pg.T;
        t.Db.Shifts.Add(new Shift
        {
            Id = Guid.NewGuid(), BranchId = t.Branch.Id, Role = "Dog walking", Capacity = 2,
            StartsAt = t.Clock.UtcNow.AddDays(1), EndsAt = t.Clock.UtcNow.AddDays(1).AddHours(-1)
        });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => t.Db.SaveChangesAsync());
        Assert.Contains("CK_Shifts_EndsAfterStart", ex.InnerException!.Message);
    }

    [PostgresFact]
    public async Task Database_rejects_an_undefined_status_value()
    {
        using var pg = new PostgresTestDb();
        var t = pg.T;
        // Bypass the enum type entirely, as a buggy client or hand-written SQL might.
        var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            t.Db.Database.ExecuteSqlRawAsync("UPDATE \"Animals\" SET \"Status\" = '99' WHERE \"Id\" = {0}", t.Dog.Id));
        Assert.Contains("CK_Animals_Status", ex.Message);
    }

    // ---------- Unique / partial unique indexes ----------

    [PostgresFact]
    public async Task Two_animals_in_care_cannot_share_a_kennel_but_an_adopted_animals_kennel_can_be_reused()
    {
        using var pg = new PostgresTestDb();
        var t = pg.T;

        t.Db.Animals.Add(t.NewAnimal("Clash", Species.Dog, t.Dog.KennelNumber));
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => t.Db.SaveChangesAsync());
        Assert.Contains("IX_Animals_BranchId_KennelNumber_InCare", ex.InnerException!.Message);
        t.Db.ChangeTracker.Clear();

        t.Db.Animals.Add(t.NewAnimal("Old resident", Species.Cat, 500, AnimalStatus.Adopted));
        t.Db.Animals.Add(t.NewAnimal("New resident", Species.Cat, 500));
        await t.Db.SaveChangesAsync(); // allowed: the adopted animal has left the kennel
    }

    [PostgresFact]
    public async Task An_animal_has_at_most_one_primary_photo_and_deleting_it_promotes_the_next()
    {
        using var pg = new PostgresTestDb();
        var t = pg.T;
        var service = t.AnimalService();
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4 };

        var first = await service.AddPhotoAsync(t.Dog.Id, jpeg);
        var second = await service.AddPhotoAsync(t.Dog.Id, jpeg);
        Assert.True(first.Value!.IsPrimary);
        Assert.False(second.Value!.IsPrimary);

        // A second primary written directly is refused by the database.
        t.Db.AnimalPhotos.Add(new AnimalPhoto { Id = Guid.NewGuid(), AnimalId = t.Dog.Id, Data = jpeg, SizeBytes = jpeg.Length, IsPrimary = true, UploadedAt = t.Clock.UtcNow });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => t.Db.SaveChangesAsync());
        Assert.Contains("IX_AnimalPhotos_AnimalId_Primary", ex.InnerException!.Message);
        t.Db.ChangeTracker.Clear();

        Assert.True((await service.DeletePhotoAsync(t.Dog.Id, first.Value.Id)).Succeeded);
        var remaining = await t.Db.AnimalPhotos.AsNoTracking().SingleAsync(p => p.AnimalId == t.Dog.Id);
        Assert.Equal(second.Value.Id, remaining.Id);
        Assert.True(remaining.IsPrimary);
    }

    // ---------- Concurrency: two people, same moment ----------

    [PostgresFact]
    public async Task Two_simultaneous_applications_for_the_last_animal_only_one_wins()
    {
        using var pg = new PostgresTestDb();
        var t = pg.T;
        using var other = t.NewScope();

        // Both adopters load the animal while it is still Available...
        var a = t.AdoptionService().SubmitAsync(t.Adopter.Id, TestDb.ValidSubmission(t.Dog.Id));
        var b = other.AdoptionService().SubmitAsync(t.OtherAdopter.Id, TestDb.ValidSubmission(t.Dog.Id));
        var results = await Task.WhenAll(a, b);

        Assert.Single(results, r => r.Succeeded);
        var loser = Assert.Single(results, r => !r.Succeeded);
        Assert.Equal(OperationErrorKind.Conflict, loser.ErrorKind);
        Assert.Equal(1, await t.Db.AdoptionApplications.CountAsync());
    }

    [PostgresFact]
    public async Task Approve_and_withdraw_at_the_same_moment_cannot_both_succeed()
    {
        using var pg = new PostgresTestDb();
        var t = pg.T;
        var submitted = await t.AdoptionService().SubmitAsync(t.Adopter.Id, TestDb.ValidSubmission(t.Dog.Id));
        var id = submitted.Value!.Id;
        Assert.True((await t.AdoptionService().StartReviewAsync(id, t.Volunteer.Id)).Succeeded);
        t.Db.ChangeTracker.Clear();

        // The volunteer's request and the adopter's request each load the application first...
        using var adopterSide = t.NewScope();
        var volunteerCopy = await t.Applications.GetAsync(id);
        var adopterCopy = await new Infrastructure.Repositories.ApplicationRepository(adopterSide.Db).GetAsync(id);
        Assert.Equal(ApplicationStatus.UnderReview, volunteerCopy!.Status);
        Assert.Equal(ApplicationStatus.UnderReview, adopterCopy!.Status);

        // ...then both save. The second save sees a changed concurrency stamp and is refused.
        var withdraw = await adopterSide.AdoptionService().WithdrawAsync(id, t.Adopter.Id);
        var approve = await t.AdoptionService().ApproveAsync(id, t.Volunteer.Id, "Welcome!");

        Assert.True(withdraw.Succeeded);
        Assert.False(approve.Succeeded);
        Assert.Equal(OperationErrorKind.Conflict, approve.ErrorKind);

        using var check = t.NewScope();
        var final = await check.Db.AdoptionApplications.Include(x => x.StatusHistory).SingleAsync(x => x.Id == id);
        Assert.Equal(ApplicationStatus.Withdrawn, final.Status);
        Assert.DoesNotContain(final.StatusHistory, h => h.ToStatus == ApplicationStatus.Approved);
        Assert.Equal(AnimalStatus.Available, (await check.Db.Animals.SingleAsync(x => x.Id == t.Dog.Id)).Status);
    }

    [PostgresFact]
    public async Task A_double_clicked_adoption_fee_is_recorded_once()
    {
        using var pg = new PostgresTestDb();
        var t = pg.T;
        var id = (await t.AdoptionService().SubmitAsync(t.Adopter.Id, TestDb.ValidSubmission(t.Dog.Id))).Value!.Id;
        await t.AdoptionService().StartReviewAsync(id, t.Volunteer.Id);
        await t.AdoptionService().ApproveAsync(id, t.Volunteer.Id, null);
        t.Db.ChangeTracker.Clear();

        using var secondClick = t.NewScope();
        var results = await Task.WhenAll(
            t.DonationService().RecordAdoptionFeeAsync(id, 950m, t.Volunteer.Id),
            secondClick.DonationService().RecordAdoptionFeeAsync(id, 950m, t.Volunteer.Id));

        Assert.Single(results, r => r.Succeeded);
        using var check = t.NewScope();
        Assert.Equal(1, await check.Db.Donations.CountAsync(d => d.ApplicationId == id && d.Type == DonationType.AdoptionFee));
    }

    [PostgresFact]
    public async Task Many_volunteers_racing_for_a_two_place_shift_never_overbook_it()
    {
        using var pg = new PostgresTestDb();
        var t = pg.T;
        var shift = new Shift
        {
            Id = Guid.NewGuid(), BranchId = t.Branch.Id, Role = "Dog walking", Capacity = 2,
            StartsAt = t.Clock.UtcNow.AddDays(2), EndsAt = t.Clock.UtcNow.AddDays(2).AddHours(3)
        };
        t.Db.Shifts.Add(shift);
        var volunteers = Enumerable.Range(0, 6).Select(i => new ApplicationUser
        {
            Id = Guid.NewGuid(), FirstName = "Vol" + i, LastName = "Test", Email = $"v{i}@test.local", UserName = $"v{i}@test.local",
            BranchId = t.Branch.Id, IsActive = true, CreatedAt = t.Clock.UtcNow
        }).ToList();
        t.Db.Users.AddRange(volunteers);
        await t.Db.SaveChangesAsync();

        var scopes = volunteers.Select(_ => t.NewScope()).ToList();
        try
        {
            var results = await Task.WhenAll(volunteers.Select((v, i) => scopes[i].ShiftService().SignUpAsync(shift.Id, v.Id)));
            Assert.Equal(2, results.Count(r => r.Succeeded));
        }
        finally
        {
            scopes.ForEach(s => s.Dispose());
        }

        using var check = t.NewScope();
        Assert.Equal(2, await check.Db.ShiftSignups.CountAsync(s => s.ShiftId == shift.Id));
    }
}
