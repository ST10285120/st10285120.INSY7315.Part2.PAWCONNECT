using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;

namespace PawConnect.Core.Services;

/// <summary>Animal intake, profile updates, status changes and photos (administrator stories).</summary>
public class AnimalService
{
    public const int MaxPhotoBytes = 2 * 1024 * 1024;
    public const int MaxPhotosPerAnimal = 6;

    private static readonly (string From, string To)[] Palette =
    {
        ("#7C9885", "#41614F"), ("#D9A25C", "#A9682E"), ("#8FA6C9", "#4C6690"),
        ("#E8A33D", "#B5482F"), ("#C6822A", "#8C591B"), ("#5B6FA8", "#33427A")
    };

    private readonly IAnimalRepository _animals;
    private readonly IApplicationRepository _applications;
    private readonly IBranchRepository _branches;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public AnimalService(IAnimalRepository animals, IApplicationRepository applications, IBranchRepository branches, IUnitOfWork uow, IClock clock)
    {
        _animals = animals;
        _applications = applications;
        _branches = branches;
        _uow = uow;
        _clock = clock;
    }

    public async Task<OperationResult<Animal>> CreateAsync(AnimalInput input, CancellationToken ct = default)
    {
        var branchId = input.BranchId ?? (await _branches.GetDefaultAsync(ct)).Id;
        var error = await ValidateAsync(input, branchId, null, ct);
        if (error != null) return OperationResult<Animal>.Failure(error);

        var now = _clock.UtcNow;
        var colours = Palette[Math.Abs(input.Name.GetHashCode()) % Palette.Length];
        var animal = new Animal
        {
            Id = Guid.NewGuid(), BranchId = branchId, Status = AnimalStatus.Available,
            ColorFrom = colours.From, ColorTo = colours.To, CreatedAt = now, UpdatedAt = now,
            IntakeDate = input.IntakeDate ?? ShelterTime.Today(_clock)
        };
        Apply(animal, input);
        await _animals.AddAsync(animal, ct);
        if (await SaveOrConflictAsync(ct) is { } conflict) return OperationResult<Animal>.Conflict(conflict);
        return OperationResult<Animal>.Success(animal);
    }

    public async Task<OperationResult<Animal>> UpdateAsync(Guid id, AnimalInput input, CancellationToken ct = default)
    {
        var animal = await _animals.GetAsync(id, ct);
        if (animal == null) return OperationResult<Animal>.NotFound("Animal not found.");
        var branchId = input.BranchId ?? animal.BranchId;
        var error = await ValidateAsync(input, branchId, id, ct);
        if (error != null) return OperationResult<Animal>.Failure(error);

        Apply(animal, input);
        animal.BranchId = branchId;
        if (input.IntakeDate is { } intake) animal.IntakeDate = intake;
        animal.UpdatedAt = _clock.UtcNow;
        animal.ConcurrencyStamp = Guid.NewGuid();
        if (await SaveOrConflictAsync(ct) is { } conflict) return OperationResult<Animal>.Conflict(conflict);
        return OperationResult<Animal>.Success(animal);
    }

    public async Task<OperationResult> SetStatusAsync(Guid id, AnimalStatus status, CancellationToken ct = default)
    {
        var animal = await _animals.GetAsync(id, ct);
        if (animal == null) return OperationResult.NotFound("Animal not found.");
        if (!Enum.IsDefined(status)) return OperationResult.Failure("Unknown status.");
        if (status == animal.Status) return OperationResult.Success();
        // The adoption workflow owns Pending and Adopted while an application is open, so an admin
        // can't put the animal into a state that contradicts its application.
        if (await _applications.HasActiveApplicationForAnimalAsync(id, ct))
            return OperationResult.Conflict($"{animal.Name} has an open adoption application. Reject or complete it first.");
        if (status == AnimalStatus.Pending)
            return OperationResult.Conflict("Pending is set automatically when someone applies to adopt.");
        // Back in care (e.g. returned from foster): the kennel must still be free.
        if (status is AnimalStatus.Available && await _animals.KennelInUseAsync(animal.BranchId, animal.KennelNumber, animal.Id, ct))
            return OperationResult.Conflict($"Kennel {animal.KennelNumber} is now used by another animal. Edit {animal.Name}'s kennel number first.");

        animal.Status = status;
        animal.UpdatedAt = _clock.UtcNow;
        animal.ConcurrencyStamp = Guid.NewGuid();
        return await SaveOrConflictAsync(ct) is { } conflict ? OperationResult.Conflict(conflict) : OperationResult.Success();
    }

    public async Task<OperationResult<AnimalPhoto>> AddPhotoAsync(Guid animalId, byte[] data, CancellationToken ct = default)
    {
        var animal = await _animals.GetAsync(animalId, ct);
        if (animal == null) return OperationResult<AnimalPhoto>.NotFound("Animal not found.");
        if (data.Length == 0) return OperationResult<AnimalPhoto>.Failure("Please choose a photo to upload.");
        if (data.Length > MaxPhotoBytes) return OperationResult<AnimalPhoto>.Failure("Photos must be 2 MB or smaller.");

        // Check the file's actual bytes rather than trusting the file name or browser-supplied type.
        var contentType = DetectImageType(data);
        if (contentType == null) return OperationResult<AnimalPhoto>.Failure("Only JPEG, PNG or WebP images can be uploaded.");

        var existing = await _animals.GetPhotoInfosAsync(animalId, ct);
        if (existing.Count >= MaxPhotosPerAnimal) return OperationResult<AnimalPhoto>.Conflict($"An animal can have at most {MaxPhotosPerAnimal} photos.");

        var photo = new AnimalPhoto
        {
            Id = Guid.NewGuid(), AnimalId = animalId, ContentType = contentType, Data = data,
            SizeBytes = data.Length, IsPrimary = existing.Count == 0, UploadedAt = _clock.UtcNow
        };
        await _animals.AddPhotoAsync(photo, ct);
        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException) when (photo.IsPrimary)
        {
            // Two first photos uploaded at the same moment: the database allows one primary
            // photo per animal, so this one is saved as a normal gallery photo instead.
            _uow.DiscardChanges();
            photo.IsPrimary = false;
            await _animals.AddPhotoAsync(photo, ct);
            await _uow.SaveChangesAsync(ct);
        }
        return OperationResult<AnimalPhoto>.Success(photo);
    }

    public async Task<OperationResult> DeletePhotoAsync(Guid animalId, Guid photoId, CancellationToken ct = default)
    {
        var photo = await _animals.GetPhotoAsync(photoId, ct);
        if (photo == null || photo.AnimalId != animalId) return OperationResult.NotFound("Photo not found.");
        var wasPrimary = photo.IsPrimary;
        _animals.RemovePhoto(photo);
        // Delete first, then promote the next photo, so the "one primary photo per animal"
        // unique index never sees two primaries at once.
        await _uow.SaveChangesAsync(ct);

        if (wasPrimary)
        {
            var next = (await _animals.GetPhotoInfosAsync(animalId, ct)).FirstOrDefault();
            if (next != null && await _animals.GetPhotoAsync(next.Id, ct) is { } nextPhoto)
            {
                nextPhoto.IsPrimary = true;
                await _uow.SaveChangesAsync(ct);
            }
        }
        return OperationResult.Success();
    }

    public static string? DetectImageType(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) return "image/jpeg";
        if (data.Length >= 8 && data[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return "image/png";
        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data.Slice(8, 4).SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }

    private static void Apply(Animal animal, AnimalInput input)
    {
        animal.Name = input.Name.Trim();
        animal.Species = input.Species;
        animal.Breed = string.IsNullOrWhiteSpace(input.Breed) ? null : input.Breed.Trim();
        animal.AgeMonthsAtIntake = input.AgeMonths;
        animal.Size = input.Size;
        animal.GoodWithKids = input.GoodWithKids;
        animal.GoodWithOtherPets = input.GoodWithOtherPets;
        animal.IsVaccinated = input.IsVaccinated;
        animal.KennelNumber = input.KennelNumber;
        animal.Bio = input.Bio?.Trim() ?? "";
    }

    private async Task<string?> ValidateAsync(AnimalInput input, Guid branchId, Guid? existingId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 100) return "Please enter a name (up to 100 characters).";
        if (input.Breed?.Length > 100) return "Breed can be at most 100 characters.";
        if (input.AgeMonths is < 0 or > 360) return "Age must be between 0 and 360 months.";
        if (input.KennelNumber is < 1 or > 9999) return "Kennel number must be between 1 and 9999.";
        if (input.Bio?.Length > 2000) return "The bio can be at most 2000 characters.";
        if (input.IntakeDate is { } d && d > ShelterTime.Today(_clock)) return "The intake date can't be in the future.";
        if (await _branches.GetAsync(branchId, ct) == null) return "Unknown branch.";
        if (await _animals.KennelInUseAsync(branchId, input.KennelNumber, existingId, ct))
            return $"Kennel {input.KennelNumber} is already assigned to another animal in care.";
        return null;
    }

    /// <summary>Saves; on a concurrent edit or a kennel clash returns the message to show instead.</summary>
    private async Task<string?> SaveOrConflictAsync(CancellationToken ct)
    {
        try
        {
            await _uow.SaveChangesAsync(ct);
            return null;
        }
        catch (ConcurrencyConflictException)
        {
            _uow.DiscardChanges();
            return "Someone else changed this animal (or took that kennel) a moment ago. Refresh and try again.";
        }
    }
}
