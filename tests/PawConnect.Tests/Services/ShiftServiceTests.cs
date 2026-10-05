using Microsoft.EntityFrameworkCore;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Services;
using PawConnect.Infrastructure.Repositories;
using PawConnect.Tests.Support;

namespace PawConnect.Tests.Services;

public class ShiftServiceTests : IDisposable
{
    private readonly TestDb _t = new();
    public void Dispose() => _t.Dispose();

    private async Task<Shift> AddShift(int capacity, int daysAhead = 1, int startHour = 9, int hours = 3, string role = "Dog walking")
    {
        var day = new DateOnly(2026, 9, 26).AddDays(daysAhead);
        var shift = new Shift
        {
            Id = Guid.NewGuid(), BranchId = _t.Branch.Id, Role = role, Capacity = capacity,
            StartsAt = ShelterTime.ToUtc(day.ToDateTime(new TimeOnly(startHour, 0))),
            EndsAt = ShelterTime.ToUtc(day.ToDateTime(new TimeOnly(startHour + hours, 0)))
        };
        _t.Db.Shifts.Add(shift);
        await _t.Db.SaveChangesAsync();
        _t.Db.ChangeTracker.Clear();
        return shift;
    }

    [Fact]
    public async Task Volunteer_can_sign_up_and_gets_an_urgent_confirmation()
    {
        var shift = await AddShift(2);
        var result = await _t.ShiftService().SignUpAsync(shift.Id, _t.Volunteer.Id);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(1, await _t.Db.ShiftSignups.CountAsync());
        var sent = Assert.Single(_t.Notifications.Sent);
        Assert.Equal(Core.Enums.NotificationUrgency.High, sent.Urgency);
    }

    [Fact]
    public async Task A_full_shift_rejects_further_sign_ups()
    {
        var shift = await AddShift(1);
        var service = _t.ShiftService();
        Assert.True((await service.SignUpAsync(shift.Id, _t.Volunteer.Id)).Succeeded);
        var second = await service.SignUpAsync(shift.Id, _t.Admin.Id);
        Assert.False(second.Succeeded);
        Assert.Contains("full", second.Error);
    }

    [Fact]
    public async Task The_same_volunteer_cannot_book_a_shift_twice()
    {
        var shift = await AddShift(5);
        var service = _t.ShiftService();
        await service.SignUpAsync(shift.Id, _t.Volunteer.Id);
        var again = await service.SignUpAsync(shift.Id, _t.Volunteer.Id);
        Assert.False(again.Succeeded);
        Assert.Contains("already signed up", again.Error);
    }

    [Fact]
    public async Task Overlapping_shifts_are_blocked_but_back_to_back_shifts_are_fine()
    {
        var morning = await AddShift(3, startHour: 8, hours: 3);                  // 08:00–11:00
        var overlapping = await AddShift(3, startHour: 10, hours: 2, role: "Front desk"); // 10:00–12:00
        var backToBack = await AddShift(3, startHour: 11, hours: 2, role: "Cattery");    // 11:00–13:00
        var service = _t.ShiftService();

        Assert.True((await service.SignUpAsync(morning.Id, _t.Volunteer.Id)).Succeeded);
        var clash = await service.SignUpAsync(overlapping.Id, _t.Volunteer.Id);
        Assert.False(clash.Succeeded);
        Assert.Contains("another shift", clash.Error);
        Assert.True((await service.SignUpAsync(backToBack.Id, _t.Volunteer.Id)).Succeeded);
    }

    [Fact]
    public async Task Past_shifts_cannot_be_booked()
    {
        var shift = await AddShift(3, daysAhead: -1);
        Assert.False((await _t.ShiftService().SignUpAsync(shift.Id, _t.Volunteer.Id)).Succeeded);
    }

    [Fact]
    public async Task Cancelling_frees_the_place()
    {
        var shift = await AddShift(1);
        var service = _t.ShiftService();
        await service.SignUpAsync(shift.Id, _t.Volunteer.Id);
        Assert.True((await service.CancelAsync(shift.Id, _t.Volunteer.Id)).Succeeded);
        Assert.True((await service.SignUpAsync(shift.Id, _t.Admin.Id)).Succeeded);
    }

    /// <summary>Simulates another volunteer grabbing the shift at the same moment: the first save conflicts.</summary>
    private sealed class ConflictOnceUow : IUnitOfWork
    {
        private readonly IUnitOfWork _inner;
        private readonly Func<Task> _beforeFirstSave;
        private bool _fired;
        public ConflictOnceUow(IUnitOfWork inner, Func<Task> beforeFirstSave) { _inner = inner; _beforeFirstSave = beforeFirstSave; }
        public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            if (!_fired)
            {
                _fired = true;
                await _beforeFirstSave();
                throw new ConcurrencyConflictException("simulated race");
            }
            return await _inner.SaveChangesAsync(ct);
        }
        public void DiscardChanges() => _inner.DiscardChanges();
    }

    [Fact]
    public async Task A_concurrent_booking_of_the_last_place_is_retried_and_then_refused()
    {
        var shift = await AddShift(1);
        // While Priya is booking, the admin takes the last place (written through a separate context).
        _t.Uow = new ConflictOnceUow(new EfUnitOfWork(_t.Db), async () =>
        {
            using var other = new Infrastructure.Data.AppDbContext(_t.Options);
            other.ShiftSignups.Add(new ShiftSignup { Id = Guid.NewGuid(), ShiftId = shift.Id, VolunteerId = _t.Admin.Id, SignedUpAt = _t.Clock.UtcNow });
            await other.SaveChangesAsync();
        });

        var result = await _t.ShiftService().SignUpAsync(shift.Id, _t.Volunteer.Id);

        Assert.False(result.Succeeded);           // on retry it sees the shift is now full
        Assert.Contains("full", result.Error);
        Assert.Equal(1, await _t.Db.ShiftSignups.CountAsync()); // never overbooked
    }

    [Theory]
    [InlineData(12, 9, "must end after")]
    [InlineData(6, 20, "at most 12 hours")]
    public async Task Invalid_shift_times_are_rejected(int startHour, int endHour, string message)
    {
        var day = new DateTime(2026, 10, 1);
        var result = await _t.ShiftService().CreateAsync(new ShiftInput(day.AddHours(startHour), day.AddHours(endHour), "Dog walking", 3, null, null));
        Assert.False(result.Succeeded);
        Assert.Contains(message, result.Error);
    }

    [Fact]
    public async Task Shifts_are_stored_in_utc()
    {
        var result = await _t.ShiftService().CreateAsync(new ShiftInput(new DateTime(2026, 10, 1, 9, 0, 0), new DateTime(2026, 10, 1, 12, 0, 0), "Front desk", 2, null, null));
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(new DateTime(2026, 10, 1, 7, 0, 0), result.Value!.StartsAt); // 09:00 SAST = 07:00 UTC
    }
}

