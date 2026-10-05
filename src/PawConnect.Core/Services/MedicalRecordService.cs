using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;

namespace PawConnect.Core.Services;

/// <summary>Secondary flow "Log medical record" (design doc, section 4.2).</summary>
public class MedicalRecordService
{
    private readonly IMedicalRecordRepository _records;
    private readonly IAnimalRepository _animals;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public MedicalRecordService(IMedicalRecordRepository records, IAnimalRepository animals, IUnitOfWork uow, IClock clock)
    {
        _records = records;
        _animals = animals;
        _uow = uow;
        _clock = clock;
    }

    public async Task<OperationResult<MedicalRecord>> AddAsync(Guid animalId, Guid staffId, MedicalRecordInput input, CancellationToken ct = default)
    {
        var animal = await _animals.GetAsync(animalId, ct);
        if (animal == null) return OperationResult<MedicalRecord>.NotFound("Animal not found.");

        var today = ShelterTime.Today(_clock);
        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Length > 120) return OperationResult<MedicalRecord>.Failure("Please enter what was done (e.g. Rabies vaccination).");
        if (input.Details?.Length > 2000) return OperationResult<MedicalRecord>.Failure("Details can be at most 2000 characters.");
        if (input.VetName?.Length > 120) return OperationResult<MedicalRecord>.Failure("Vet name is too long.");
        if (input.RecordDate > today) return OperationResult<MedicalRecord>.Failure("The treatment date can't be in the future.");
        if (input.NextDueDate != null && input.NextDueDate < input.RecordDate) return OperationResult<MedicalRecord>.Failure("The next due date must be after the treatment date.");

        var record = new MedicalRecord
        {
            Id = Guid.NewGuid(), AnimalId = animalId, RecordedById = staffId, RecordType = input.RecordType,
            Title = input.Title.Trim(), Details = string.IsNullOrWhiteSpace(input.Details) ? null : input.Details.Trim(),
            VetName = string.IsNullOrWhiteSpace(input.VetName) ? null : input.VetName.Trim(),
            RecordDate = input.RecordDate, NextDueDate = input.NextDueDate, CreatedAt = _clock.UtcNow
        };

        // Logging a vaccination updates the vaccination badge on the public profile straight away.
        if (input.RecordType == MedicalRecordType.Vaccination && !animal.IsVaccinated)
        {
            animal.IsVaccinated = true;
            animal.UpdatedAt = _clock.UtcNow;
            animal.ConcurrencyStamp = Guid.NewGuid();
        }

        await _records.AddAsync(record, ct);
        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            _uow.DiscardChanges();
            return OperationResult<MedicalRecord>.Conflict("Someone else updated this animal a moment ago. Please save the record again.");
        }
        return OperationResult<MedicalRecord>.Success(record);
    }
}
