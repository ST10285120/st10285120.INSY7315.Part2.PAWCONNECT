using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Notifications;
using PawConnect.Core.Receipts;

namespace PawConnect.Core.Services;

/// <summary>
/// Online donations and sponsorships. Card payments are out of scope, so a donation is
/// recorded as a pledge with a receipt number. Staff mark it Received once the money clears.
/// </summary>
public class DonationService
{
    public const decimal MinimumAmount = 10m;
    public const decimal MaximumAmount = 100_000m;

    private readonly IDonationRepository _donations;
    private readonly IAnimalRepository _animals;
    private readonly IApplicationRepository _applications;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly IDonationReceiptFactory _receipts;
    private readonly INotificationService _notifications;
    private readonly IClock _clock;

    public DonationService(IDonationRepository donations, IAnimalRepository animals, IApplicationRepository applications,
        IUserRepository users, IUnitOfWork uow, IDonationReceiptFactory receipts, INotificationService notifications, IClock clock)
    {
        _donations = donations;
        _animals = animals;
        _applications = applications;
        _users = users;
        _uow = uow;
        _receipts = receipts;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task<OperationResult<Donation>> PledgeAsync(Guid donorId, DonationPledge pledge, CancellationToken ct = default)
    {
        if (pledge.Amount < MinimumAmount || pledge.Amount > MaximumAmount)
            return OperationResult<Donation>.Failure($"Please enter an amount between R{MinimumAmount:0} and R{MaximumAmount:N0}.");
        if (decimal.Round(pledge.Amount, 2) != pledge.Amount)
            return OperationResult<Donation>.Failure("Amounts can have at most two decimal places.");
        if (pledge.Note?.Length > 500) return OperationResult<Donation>.Failure("The note can be at most 500 characters.");

        Animal? animal = null;
        if (pledge.SponsorAnimalId is { } animalId)
        {
            animal = await _animals.GetAsync(animalId, ct);
            if (animal == null || animal.Status == AnimalStatus.Adopted)
                return OperationResult<Donation>.Conflict("That animal can't be sponsored right now. Maybe they've found a home!");
        }

        var donation = new Donation
        {
            Id = Guid.NewGuid(), DonorId = donorId, AnimalId = animal?.Id, Amount = pledge.Amount,
            Type = animal == null ? DonationType.General : DonationType.Sponsorship,
            Frequency = pledge.Frequency, Status = DonationStatus.Pledged, DonatedAt = _clock.UtcNow,
            ReceiptNumber = await NewUniqueReceiptNumberAsync(ct),
            Note = string.IsNullOrWhiteSpace(pledge.Note) ? null : pledge.Note.Trim()
        };
        await _donations.AddAsync(donation, ct);
        await _uow.SaveChangesAsync(ct);

        await SendReceiptAsync(donation, animal?.Name, ct);
        return OperationResult<Donation>.Success(donation);
    }

    /// <summary>Staff record the adoption fee paid at the shelter. Required before an adoption can complete.</summary>
    public async Task<OperationResult<Donation>> RecordAdoptionFeeAsync(Guid applicationId, decimal amount, Guid staffId, CancellationToken ct = default)
    {
        var app = await _applications.GetAsync(applicationId, ct);
        if (app == null) return OperationResult<Donation>.NotFound("Application not found.");
        if (app.Status != ApplicationStatus.Approved) return OperationResult<Donation>.Conflict("The adoption fee can only be recorded for an approved application.");
        if (await _donations.HasReceivedAdoptionFeeAsync(app.Id, ct)) return OperationResult<Donation>.Conflict("The adoption fee has already been recorded.");
        if (amount <= 0 || amount > MaximumAmount) return OperationResult<Donation>.Failure("Please enter a valid fee amount.");

        var now = _clock.UtcNow;
        var donation = new Donation
        {
            Id = Guid.NewGuid(), DonorId = app.AdopterId, AnimalId = app.AnimalId, ApplicationId = app.Id,
            Amount = decimal.Round(amount, 2), Type = DonationType.AdoptionFee, Frequency = DonationFrequency.OnceOff,
            Status = DonationStatus.Received, DonatedAt = now, ReceivedAt = now, RecordedById = staffId,
            ReceiptNumber = await NewUniqueReceiptNumberAsync(ct)
        };
        await _donations.AddAsync(donation, ct);
        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // A double-click or a second staff member: the database allows one adoption fee per application.
            _uow.DiscardChanges();
            return OperationResult<Donation>.Conflict("The adoption fee has already been recorded.");
        }
        return OperationResult<Donation>.Success(donation);
    }

    public async Task<OperationResult> MarkReceivedAsync(Guid donationId, Guid staffId, CancellationToken ct = default)
    {
        var donation = await _donations.GetAsync(donationId, ct);
        if (donation == null) return OperationResult.NotFound("Donation not found.");
        if (donation.Status != DonationStatus.Pledged) return OperationResult.Conflict("Only pledged donations can be marked as received.");

        donation.Status = DonationStatus.Received;
        donation.ReceivedAt = _clock.UtcNow;
        donation.RecordedById = staffId;
        await _uow.SaveChangesAsync(ct);
        await SendReceiptAsync(donation, donation.Animal?.Name, ct);
        return OperationResult.Success();
    }

    public async Task<OperationResult> CancelAsync(Guid donationId, Guid staffId, CancellationToken ct = default)
    {
        var donation = await _donations.GetAsync(donationId, ct);
        if (donation == null) return OperationResult.NotFound("Donation not found.");
        if (donation.Status != DonationStatus.Pledged) return OperationResult.Conflict("Only pledged donations can be cancelled.");
        donation.Status = DonationStatus.Cancelled;
        donation.RecordedById = staffId;
        await _uow.SaveChangesAsync(ct);
        return OperationResult.Success();
    }

    /// <summary>Returns the receipt, but only to its donor or to staff (ownership check).</summary>
    public async Task<DonationReceipt?> GetReceiptAsync(Guid donationId, Guid requesterId, bool requesterIsStaff, CancellationToken ct = default)
    {
        var donation = await _donations.GetAsync(donationId, ct);
        if (donation == null || (!requesterIsStaff && donation.DonorId != requesterId)) return null;
        var donor = donation.Donor ?? await _users.GetAsync(donation.DonorId, ct);
        return _receipts.Create(donation, donor?.FullName ?? "", donation.Animal?.Name);
    }

    private async Task SendReceiptAsync(Donation donation, string? animalName, CancellationToken ct)
    {
        var donor = donation.Donor ?? await _users.GetAsync(donation.DonorId, ct);
        var receipt = _receipts.Create(donation, donor?.FullName ?? "", animalName);
        await _notifications.SendAsync(NotificationTemplates.DonationReceipt(receipt, donor?.Email, donation.DonorId), ct);
    }

    private async Task<string> NewUniqueReceiptNumberAsync(CancellationToken ct)
    {
        var number = _receipts.NewReceiptNumber(_clock.UtcNow);
        for (var i = 0; i < 5 && await _donations.ReceiptNumberExistsAsync(number, ct); i++)
            number = _receipts.NewReceiptNumber(_clock.UtcNow);
        return number;
    }
}
