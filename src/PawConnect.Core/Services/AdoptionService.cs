using Microsoft.Extensions.Logging;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Notifications;

namespace PawConnect.Core.Services;

/// <summary>
/// Adoption workflow: submit, review, approve or reject, home visit, adopted.
/// Keeps each animal's status in step with its application, as the state diagram requires.
/// </summary>
public class AdoptionService
{
    private readonly IApplicationRepository _applications;
    private readonly IAnimalRepository _animals;
    private readonly IDonationRepository _donations;
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notifications;
    private readonly IClock _clock;
    private readonly ILogger<AdoptionService> _logger;

    public AdoptionService(
        IApplicationRepository applications,
        IAnimalRepository animals,
        IDonationRepository donations,
        IUnitOfWork uow,
        INotificationService notifications,
        IClock clock,
        ILogger<AdoptionService> logger)
    {
        _applications = applications;
        _animals = animals;
        _donations = donations;
        _uow = uow;
        _notifications = notifications;
        _clock = clock;
        _logger = logger;
    }

    public async Task<OperationResult<AdoptionApplication>> SubmitAsync(Guid adopterId, ApplicationSubmission input, CancellationToken ct = default)
    {
        var error = Validate(input);
        if (error != null) return OperationResult<AdoptionApplication>.Failure(error);

        var animal = await _animals.GetAsync(input.AnimalId, ct);
        if (animal == null)
            return OperationResult<AdoptionApplication>.NotFound("That animal could not be found.");

        // Acceptance criterion: an animal that is pending or adopted can't receive new applications.
        if (animal.Status != AnimalStatus.Available || await _applications.HasActiveApplicationForAnimalAsync(animal.Id, ct))
            return OperationResult<AdoptionApplication>.Conflict($"{animal.Name} is no longer available for adoption.");

        var now = _clock.UtcNow;
        var reference = ReferenceGenerator.NewApplicationReference(now);
        for (var i = 0; i < 5 && await _applications.ReferenceExistsAsync(reference, ct); i++)
            reference = ReferenceGenerator.NewApplicationReference(now);

        var application = new AdoptionApplication
        {
            Id = Guid.NewGuid(),
            ReferenceNumber = reference,
            AnimalId = animal.Id,
            AdopterId = adopterId,
            Status = ApplicationStatus.Submitted,
            SubmittedAt = now,
            FullName = input.FullName.Trim(),
            Phone = input.Phone.Trim(),
            Email = input.Email.Trim(),
            Why = input.Why.Trim(),
            HomeType = input.HomeType,
            OwnRent = input.OwnRent,
            OtherPets = input.OtherPets,
            Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim()
        };
        application.StatusHistory.Add(new ApplicationStatusChange
        {
            Id = Guid.NewGuid(), ApplicationId = application.Id, FromStatus = null,
            ToStatus = ApplicationStatus.Submitted, ChangedById = adopterId, ChangedAt = now
        });

        animal.Status = AnimalStatus.Pending;
        animal.UpdatedAt = now;
        animal.ConcurrencyStamp = Guid.NewGuid();

        await _applications.AddAsync(application, ct);
        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // Someone else applied for this animal at the same moment: the animal's concurrency
            // stamp, or the database's unique "one open application per animal" index, rejected
            // the second save.
            _uow.DiscardChanges();
            return OperationResult<AdoptionApplication>.Conflict($"{animal.Name} is no longer available for adoption.");
        }
        _logger.LogInformation("Application {Reference} submitted for animal {AnimalId}", reference, animal.Id);

        await _notifications.SendAsync(NotificationTemplates.ApplicationReceived(application, animal.Name), ct);
        return OperationResult<AdoptionApplication>.Success(application);
    }

    public Task<OperationResult> StartReviewAsync(Guid applicationId, Guid staffId, CancellationToken ct = default) =>
        TransitionAsync(applicationId, ApplicationStatus.UnderReview, staffId, null, ct);

    public Task<OperationResult> ApproveAsync(Guid applicationId, Guid staffId, string? note, CancellationToken ct = default) =>
        TransitionAsync(applicationId, ApplicationStatus.Approved, staffId, note, ct);

    public Task<OperationResult> RejectAsync(Guid applicationId, Guid staffId, string? note, CancellationToken ct = default) =>
        TransitionAsync(applicationId, ApplicationStatus.Rejected, staffId, note, ct);

    public Task<OperationResult> CompleteAdoptionAsync(Guid applicationId, Guid staffId, CancellationToken ct = default) =>
        TransitionAsync(applicationId, ApplicationStatus.Adopted, staffId, null, ct);

    /// <summary>The adopter withdraws their own application.</summary>
    public async Task<OperationResult> WithdrawAsync(Guid applicationId, Guid adopterId, CancellationToken ct = default)
    {
        var app = await _applications.GetAsync(applicationId, ct);
        // Ownership check is done server-side, never only by hiding the button (OWASP A01).
        if (app == null || app.AdopterId != adopterId)
            return OperationResult.NotFound("Application not found.");
        return await TransitionAsync(app, ApplicationStatus.Withdrawn, adopterId, "Withdrawn by applicant", ct);
    }

    public async Task<OperationResult> RecordHomeVisitAsync(Guid applicationId, Guid staffId, string? notes, CancellationToken ct = default)
    {
        var app = await _applications.GetAsync(applicationId, ct);
        if (app == null) return OperationResult.NotFound("Application not found.");
        if (app.Status != ApplicationStatus.Approved)
            return OperationResult.Conflict("A home visit can only be recorded once the application is approved.");

        app.HomeVisitCompletedAt = _clock.UtcNow;
        app.HomeVisitNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        app.ConcurrencyStamp = Guid.NewGuid();
        AddHistory(app, app.Status, app.Status, staffId, "Home visit completed");
        return await SaveOrConflictAsync(ct) ?? OperationResult.Success();
    }

    private async Task<OperationResult> TransitionAsync(Guid applicationId, ApplicationStatus to, Guid actorId, string? note, CancellationToken ct)
    {
        var app = await _applications.GetAsync(applicationId, ct);
        if (app == null) return OperationResult.NotFound("Application not found.");
        return await TransitionAsync(app, to, actorId, note, ct);
    }

    private async Task<OperationResult> TransitionAsync(AdoptionApplication app, ApplicationStatus to, Guid actorId, string? note, CancellationToken ct)
    {
        var from = app.Status;
        if (!ApplicationStateMachine.CanTransition(from, to))
            return OperationResult.Conflict($"An application that is {from.Label()} can't be moved to {to.Label()}.");

        // Guard conditions for completing an adoption (design doc, section 4.3).
        if (to == ApplicationStatus.Adopted)
        {
            if (app.HomeVisitCompletedAt == null)
                return OperationResult.Conflict("Record the home visit before completing the adoption.");
            if (!await _donations.HasReceivedAdoptionFeeAsync(app.Id, ct))
                return OperationResult.Conflict("Record the adoption fee as received before completing the adoption.");
        }

        var now = _clock.UtcNow;
        app.Status = to;
        if (to is ApplicationStatus.UnderReview or ApplicationStatus.Approved or ApplicationStatus.Rejected)
            app.ReviewedById = actorId;
        if (to is ApplicationStatus.Approved or ApplicationStatus.Rejected)
        {
            app.DecidedAt = now;
            if (!string.IsNullOrWhiteSpace(note)) app.ReviewNotes = note.Trim();
        }
        if (to is ApplicationStatus.Adopted or ApplicationStatus.Withdrawn or ApplicationStatus.Rejected)
            app.CompletedAt = now;

        app.ConcurrencyStamp = Guid.NewGuid();
        AddHistory(app, from, to, actorId, note);

        // Keep the animal's public status in sync with the application.
        var animal = app.Animal ?? await _animals.GetAsync(app.AnimalId, ct);
        if (animal != null)
        {
            if (to == ApplicationStatus.Adopted)
                animal.Status = AnimalStatus.Adopted;
            else if (to is ApplicationStatus.Rejected or ApplicationStatus.Withdrawn && animal.Status == AnimalStatus.Pending)
                animal.Status = AnimalStatus.Available;
            animal.UpdatedAt = now;
            animal.ConcurrencyStamp = Guid.NewGuid();
        }

        var conflict = await SaveOrConflictAsync(ct);
        if (conflict != null) return conflict;
        _logger.LogInformation("Application {Reference} moved {From} -> {To}", app.ReferenceNumber, from, to);

        var animalName = animal?.Name ?? "your animal";
        var message = to switch
        {
            ApplicationStatus.Approved => NotificationTemplates.ApplicationApproved(app, animalName),
            ApplicationStatus.Rejected => NotificationTemplates.ApplicationRejected(app, animalName),
            ApplicationStatus.Adopted => NotificationTemplates.AdoptionCompleted(app, animalName),
            _ => null
        };
        if (message != null) await _notifications.SendAsync(message, ct);

        return OperationResult.Success();
    }

    /// <summary>
    /// Saves, or returns a 409-style result if someone else changed the application (or its
    /// animal) since it was loaded. Nothing is half-saved: the whole change is discarded.
    /// </summary>
    private async Task<OperationResult?> SaveOrConflictAsync(CancellationToken ct)
    {
        try
        {
            await _uow.SaveChangesAsync(ct);
            return null;
        }
        catch (ConcurrencyConflictException)
        {
            _uow.DiscardChanges();
            _logger.LogWarning("Concurrent update to an adoption application was rejected");
            return OperationResult.Conflict("Someone else updated this application a moment ago. Refresh the page to see its latest status.");
        }
    }

    /// <summary>
    /// Appends an audit entry. The Id is deliberately left empty so EF Core generates it and
    /// treats the row as new. (A pre-set key on a child added to an already-loaded parent would
    /// be mistaken for an existing row and produce an UPDATE instead of an INSERT.)
    /// </summary>
    private void AddHistory(AdoptionApplication app, ApplicationStatus? from, ApplicationStatus to, Guid actorId, string? note) =>
        app.StatusHistory.Add(new ApplicationStatusChange
        {
            ApplicationId = app.Id, FromStatus = from, ToStatus = to, ChangedById = actorId,
            ChangedAt = _clock.UtcNow, Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
        });

    private static string? Validate(ApplicationSubmission input)
    {
        if (string.IsNullOrWhiteSpace(input.FullName) || input.FullName.Length > 120) return "Please enter your full name.";
        if (string.IsNullOrWhiteSpace(input.Phone) || input.Phone.Length > 30) return "Please enter a phone number.";
        if (string.IsNullOrWhiteSpace(input.Email) || !input.Email.Contains('@') || input.Email.Length > 200) return "Please enter a valid email address.";
        if (string.IsNullOrWhiteSpace(input.Why) || input.Why.Length > 2000) return "Please tell us why you'd like to adopt (up to 2000 characters).";
        if (string.IsNullOrWhiteSpace(input.HomeType) || string.IsNullOrWhiteSpace(input.OwnRent) || string.IsNullOrWhiteSpace(input.OtherPets))
            return "Please complete the questions about your home.";
        if (input.Notes?.Length > 2000) return "Notes can be at most 2000 characters.";
        return null;
    }
}
