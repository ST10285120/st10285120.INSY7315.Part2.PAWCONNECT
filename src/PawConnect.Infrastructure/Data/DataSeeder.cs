using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Receipts;

namespace PawConnect.Infrastructure.Data;

/// <summary>
/// Runs at start-up: applies EF Core migrations, then makes sure the roles, the default branch
/// and the first administrator exist. With Seed:DemoData=true (development and staging only) it
/// also loads the demo animals, volunteers, shifts and donations from the prototype.
/// </summary>
public class DataSeeder
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole<Guid>> _roles;
    private readonly IConfiguration _config;
    private readonly IClock _clock;
    private readonly IDonationReceiptFactory _receipts;
    private readonly ILogger<DataSeeder> _logger;

    public DataSeeder(AppDbContext db, UserManager<ApplicationUser> users, RoleManager<IdentityRole<Guid>> roles,
        IConfiguration config, IClock clock, IDonationReceiptFactory receipts, ILogger<DataSeeder> logger)
    {
        _db = db;
        _users = users;
        _roles = roles;
        _config = config;
        _clock = clock;
        _receipts = receipts;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        if (_db.Database.IsRelational())
        {
            if (_config.GetValue("Database:MigrateOnStartup", true))
                await _db.Database.MigrateAsync(ct);
        }
        else
        {
            await _db.Database.EnsureCreatedAsync(ct);
        }

        foreach (var role in Roles.All)
            if (!await _roles.RoleExistsAsync(role))
                await _roles.CreateAsync(new IdentityRole<Guid>(role));

        var branch = await _db.Branches.OrderBy(b => b.Name).FirstOrDefaultAsync(ct);
        if (branch == null)
        {
            branch = new ShelterBranch { Id = Guid.NewGuid(), Name = "Muizenberg", Location = "Muizenberg, Cape Town" };
            _db.Branches.Add(branch);
            await _db.SaveChangesAsync(ct);
        }

        var adminEmail = _config["Seed:AdminEmail"];
        var adminPassword = _config["Seed:AdminPassword"];
        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
            await EnsureUserAsync(adminEmail, adminPassword, "Shelter", "Manager", Roles.Administrator, branch.Id, null);

        if (_config.GetValue<bool>("Seed:DemoData"))
        {
            if (!await _db.Animals.AnyAsync(ct))
                await SeedDemoDataAsync(branch, ct);

            // Also runs on databases seeded before the photos were added, so an existing
            // local database picks them up on the next start without a reset.
            await AttachDemoPhotosAsync(ct);
        }
    }

    /// <summary>
    /// Gives each demo animal that has no photo yet its real photo from the embedded
    /// Data/SeedPhotos/{name}.jpg resource. Animals an admin has already given photos are left alone.
    /// </summary>
    private async Task AttachDemoPhotosAsync(CancellationToken ct)
    {
        var assembly = typeof(DataSeeder).Assembly;
        var withoutPhotos = await _db.Animals
            .Where(a => !_db.AnimalPhotos.Any(p => p.AnimalId == a.Id))
            .Select(a => new { a.Id, a.Name })
            .ToListAsync(ct);

        var added = 0;
        foreach (var animal in withoutPhotos)
        {
            await using var stream = assembly.GetManifestResourceStream($"SeedPhotos.{animal.Name.ToLowerInvariant()}.jpg");
            if (stream == null) continue;
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            var data = buffer.ToArray();
            _db.AnimalPhotos.Add(new AnimalPhoto
            {
                Id = Guid.NewGuid(), AnimalId = animal.Id, ContentType = "image/jpeg",
                Data = data, SizeBytes = data.Length, IsPrimary = true, UploadedAt = _clock.UtcNow
            });
            added++;
        }

        if (added > 0)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Attached {Count} demo animal photos", added);
        }
    }

    private async Task<ApplicationUser?> EnsureUserAsync(string email, string password, string first, string last,
        string role, Guid? branchId, string? volunteerRole, string? phone = null, DateTime? createdAt = null, bool active = true)
    {
        var user = await _users.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true,
                FirstName = first, LastName = last, PhoneNumber = phone, BranchId = branchId,
                VolunteerRole = volunteerRole, IsActive = active,
                CreatedAt = createdAt ?? _clock.UtcNow, ConsentAcceptedAt = createdAt ?? _clock.UtcNow
            };
            var result = await _users.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                _logger.LogError("Could not create seed user {Email}: {Errors}", email, string.Join("; ", result.Errors.Select(e => e.Description)));
                return null;
            }
        }
        if (!await _users.IsInRoleAsync(user, role))
            await _users.AddToRoleAsync(user, role);
        return user;
    }

    private async Task SeedDemoDataAsync(ShelterBranch branch, CancellationToken ct)
    {
        var password = _config["Seed:DemoPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("Seed:DemoData is on but Seed:DemoPassword is empty; skipping demo data.");
            return;
        }
        _logger.LogInformation("Seeding demo data");
        var now = _clock.UtcNow;
        var today = ShelterTime.Today(_clock);

        // Demo accounts (the same volunteers as the Task 1 prototype).
        var priya = await EnsureUserAsync("priya@pawconnect.demo", password, "Priya", "Naidoo", Roles.Volunteer, branch.Id, "Dog walking", "082 111 2222", new DateTime(2025, 6, 12, 8, 0, 0, DateTimeKind.Utc));
        var sipho = await EnsureUserAsync("sipho@pawconnect.demo", password, "Sipho", "Mokoena", Roles.Volunteer, branch.Id, "Cattery cleaning", "083 222 3333", new DateTime(2025, 9, 3, 8, 0, 0, DateTimeKind.Utc));
        var emma = await EnsureUserAsync("emma@pawconnect.demo", password, "Emma", "van der Merwe", Roles.Volunteer, branch.Id, "Front desk", "084 333 4444", new DateTime(2024, 11, 20, 8, 0, 0, DateTimeKind.Utc));
        var thabo = await EnsureUserAsync("thabo@pawconnect.demo", password, "Thabo", "Nkosi", Roles.Volunteer, branch.Id, "Dog walking", "072 444 5555", new DateTime(2026, 1, 15, 8, 0, 0, DateTimeKind.Utc));
        await EnsureUserAsync("aisha@pawconnect.demo", password, "Aisha", "Adams", Roles.Volunteer, branch.Id, "Events & fundraising", "071 555 6666", new DateTime(2025, 3, 8, 8, 0, 0, DateTimeKind.Utc), active: false);
        var admin = await EnsureUserAsync("admin@pawconnect.demo", password, "Lindiwe", "Dlamini", Roles.Administrator, branch.Id, null, "021 555 0100");
        var adopter = await EnsureUserAsync("adopter@pawconnect.demo", password, "Jordan", "Adams", Roles.Adopter, null, null, "079 123 4567");
        if (priya == null || sipho == null || emma == null || thabo == null || admin == null || adopter == null) return;

        Animal A(string name, Species species, string breed, int ageMonths, AnimalSize size, AnimalStatus status, bool vacc,
            int kennel, string from, string to, bool kids, bool pets, int daysInCare, string bio) => new()
        {
            Id = Guid.NewGuid(), BranchId = branch.Id, Name = name, Species = species, Breed = breed,
            AgeMonthsAtIntake = ageMonths, Size = size, Status = status, IsVaccinated = vacc, KennelNumber = kennel,
            ColorFrom = from, ColorTo = to, GoodWithKids = kids, GoodWithOtherPets = pets, Bio = bio,
            IntakeDate = today.AddDays(-daysInCare), CreatedAt = now, UpdatedAt = now
        };

        var biscuit = A("Biscuit", Species.Dog, "Cross-breed", 24, AnimalSize.Medium, AnimalStatus.Available, true, 214, "#7C9885", "#41614F", true, true, 40,
            "Biscuit is a gentle, food-motivated boy who loves belly rubs and long walks. Great with kids and other dogs.");
        var whiskers = A("Whiskers", Species.Cat, "Tabby", 12, AnimalSize.Small, AnimalStatus.Available, true, 118, "#D9A25C", "#A9682E", false, true, 25,
            "Whiskers is curious and independent, ideal for a quiet home. Loves sunny windowsills and gentle play.");
        var nala = A("Nala", Species.Dog, "Labrador mix", 48, AnimalSize.Large, AnimalStatus.Available, true, 97, "#C6822A", "#8C591B", true, false, 60,
            "Nala is a calm, well-trained girl looking for a relaxed home. House-trained and good on a leash.");
        var milo = A("Milo", Species.Cat, "Domestic shorthair", 6, AnimalSize.Small, AnimalStatus.Available, false, 203, "#8FA6C9", "#4C6690", true, true, 14,
            "Milo is a playful kitten still completing his vaccination course. Bonded with his sister Mochi, so ideally adopted together.");
        var mochi = A("Mochi", Species.Cat, "Domestic shorthair", 6, AnimalSize.Small, AnimalStatus.Available, false, 204, "#5B6FA8", "#33427A", true, true, 14,
            "Mochi is Milo's shy sister. She warms up with patience and treats, then follows you everywhere.");
        var rocky = A("Rocky", Species.Dog, "Boerboel mix", 36, AnimalSize.Large, AnimalStatus.Adopted, true, 56, "#41614F", "#1F3A2E", true, false, 200,
            "Rocky found his forever home! Big, gentle, and loyal. He settled in within days.");
        var pumpkin = A("Pumpkin", Species.Cat, "Ginger", 24, AnimalSize.Medium, AnimalStatus.Available, true, 141, "#E8A33D", "#B5482F", true, false, 33,
            "Pumpkin is a vocal, affectionate cat who'll happily supervise everything you do around the house.");
        var duke = A("Duke", Species.Dog, "Staffie cross", 60, AnimalSize.Medium, AnimalStatus.Fostered, true, 88, "#C6822A", "#6E4518", false, false, 90,
            "Duke is recovering from a leg injury with a foster family. He'll be back up for adoption soon.");
        var luna = A("Luna", Species.Dog, "Jack Russell", 18, AnimalSize.Small, AnimalStatus.Available, true, 172, "#8FA6C9", "#41614F", true, true, 8,
            "Luna has endless energy and a big heart. Perfect for an active family who loves the outdoors.");
        var animals = new[] { biscuit, whiskers, nala, milo, mochi, rocky, pumpkin, duke, luna };
        _db.Animals.AddRange(animals);

        // Medical history.
        MedicalRecord M(Animal a, MedicalRecordType t, string title, int daysAgo, string? details = null, int? dueInDays = null) => new()
        {
            Id = Guid.NewGuid(), AnimalId = a.Id, RecordedById = priya.Id, RecordType = t, Title = title, Details = details,
            VetName = "Dr K. Petersen", RecordDate = today.AddDays(-daysAgo),
            NextDueDate = dueInDays is { } d ? today.AddDays(d) : null, CreatedAt = now
        };
        _db.MedicalRecords.AddRange(
            M(biscuit, MedicalRecordType.Vaccination, "5-in-1 booster", 30, "Annual booster, no reaction.", 335),
            M(biscuit, MedicalRecordType.Sterilisation, "Neutered", 35),
            M(whiskers, MedicalRecordType.Vaccination, "Feline 3-in-1", 20, null, 345),
            M(milo, MedicalRecordType.Vaccination, "First kitten vaccine", 10, "Second dose due in 3 weeks.", 11),
            M(nala, MedicalRecordType.Checkup, "Hip assessment", 50, "Mild stiffness; joint supplement recommended."),
            M(duke, MedicalRecordType.Treatment, "Leg splint", 20, "Hairline fracture of the front left leg. Re-check in 4 weeks.", 8));

        // Adopted application for Rocky, with the full audit trail and adoption fee.
        var submitted = now.AddDays(-40);
        var rockyApp = new AdoptionApplication
        {
            Id = Guid.NewGuid(), ReferenceNumber = "PC-" + ShelterTime.ToLocal(submitted).ToString("yyMMdd") + "-RCKY",
            AnimalId = rocky.Id, AdopterId = adopter.Id, ReviewedById = priya.Id, Status = ApplicationStatus.Adopted,
            SubmittedAt = submitted, DecidedAt = submitted.AddHours(30), CompletedAt = submitted.AddDays(6),
            FullName = adopter.FullName, Phone = adopter.PhoneNumber ?? "", Email = adopter.Email!, Why = "We have a big garden and lots of love to give.",
            HomeType = "House with garden", OwnRent = "Own", OtherPets = "None", HomeVisitCompletedAt = submitted.AddDays(4),
            HomeVisitNotes = "Secure garden, family met Rocky twice."
        };
        rockyApp.StatusHistory.AddRange(new[]
        {
            new ApplicationStatusChange { Id = Guid.NewGuid(), ApplicationId = rockyApp.Id, ToStatus = ApplicationStatus.Submitted, ChangedById = adopter.Id, ChangedAt = submitted },
            new ApplicationStatusChange { Id = Guid.NewGuid(), ApplicationId = rockyApp.Id, FromStatus = ApplicationStatus.Submitted, ToStatus = ApplicationStatus.UnderReview, ChangedById = priya.Id, ChangedAt = submitted.AddHours(5) },
            new ApplicationStatusChange { Id = Guid.NewGuid(), ApplicationId = rockyApp.Id, FromStatus = ApplicationStatus.UnderReview, ToStatus = ApplicationStatus.Approved, ChangedById = priya.Id, ChangedAt = submitted.AddHours(30) },
            new ApplicationStatusChange { Id = Guid.NewGuid(), ApplicationId = rockyApp.Id, FromStatus = ApplicationStatus.Approved, ToStatus = ApplicationStatus.Adopted, ChangedById = admin.Id, ChangedAt = submitted.AddDays(6) },
        });
        _db.AdoptionApplications.Add(rockyApp);

        // Donations over the last six months (received), plus one open pledge.
        var rng = new Random(42);
        var donations = new List<Donation>();
        for (var m = 5; m >= 0; m--)
        {
            var monthStart = new DateTime(now.Year, now.Month, 1, 8, 0, 0, DateTimeKind.Utc).AddMonths(-m);
            for (var i = 0; i < 6; i++)
            {
                var at = monthStart.AddDays(rng.Next(0, 27)).AddHours(rng.Next(0, 9));
                if (at > now) at = now.AddHours(-1);
                var sponsor = i % 3 == 0 ? animals[rng.Next(animals.Length)] : null;
                if (sponsor?.Status == AnimalStatus.Adopted) sponsor = null;
                donations.Add(new Donation
                {
                    Id = Guid.NewGuid(), DonorId = adopter.Id, AnimalId = sponsor?.Id, Amount = new[] { 100m, 250m, 500m, 1000m, 1500m }[rng.Next(5)] + m * 50,
                    Type = sponsor == null ? DonationType.General : DonationType.Sponsorship, Frequency = DonationFrequency.OnceOff,
                    Status = DonationStatus.Received, DonatedAt = at, ReceivedAt = at, RecordedById = admin.Id,
                    ReceiptNumber = _receipts.NewReceiptNumber(at)
                });
            }
        }
        donations.Add(new Donation
        {
            Id = Guid.NewGuid(), DonorId = adopter.Id, ApplicationId = rockyApp.Id, AnimalId = rocky.Id, Amount = 950m,
            Type = DonationType.AdoptionFee, Frequency = DonationFrequency.OnceOff, Status = DonationStatus.Received,
            DonatedAt = submitted.AddDays(5), ReceivedAt = submitted.AddDays(5), RecordedById = admin.Id,
            ReceiptNumber = _receipts.NewReceiptNumber(submitted.AddDays(5))
        });
        donations.Add(new Donation
        {
            Id = Guid.NewGuid(), DonorId = adopter.Id, AnimalId = biscuit.Id, Amount = 250m, Type = DonationType.Sponsorship,
            Frequency = DonationFrequency.Monthly, Status = DonationStatus.Pledged, DonatedAt = now.AddDays(-1),
            ReceiptNumber = _receipts.NewReceiptNumber(now.AddDays(-1))
        });
        _db.Donations.AddRange(donations);

        // Upcoming shifts, some partly filled.
        Shift S(int daysAhead, int startHour, int hours, string role, int capacity) => new()
        {
            Id = Guid.NewGuid(), BranchId = branch.Id, Role = role, Capacity = capacity,
            StartsAt = ShelterTime.ToUtc(today.AddDays(daysAhead).ToDateTime(new TimeOnly(startHour, 0))),
            EndsAt = ShelterTime.ToUtc(today.AddDays(daysAhead).ToDateTime(new TimeOnly(startHour + hours, 0)))
        };
        var s1 = S(1, 8, 3, "Dog walking", 4);
        var s2 = S(1, 11, 3, "Cattery cleaning", 2);
        var s3 = S(2, 9, 3, "Front desk", 2);
        var s4 = S(5, 15, 2, "Dog walking", 3);
        var s5 = S(7, 10, 4, "Adoption day event", 6);
        _db.Shifts.AddRange(s1, s2, s3, s4, s5);
        _db.ShiftSignups.AddRange(
            new ShiftSignup { Id = Guid.NewGuid(), ShiftId = s1.Id, VolunteerId = sipho.Id, SignedUpAt = now },
            new ShiftSignup { Id = Guid.NewGuid(), ShiftId = s1.Id, VolunteerId = thabo.Id, SignedUpAt = now },
            new ShiftSignup { Id = Guid.NewGuid(), ShiftId = s1.Id, VolunteerId = emma.Id, SignedUpAt = now },
            new ShiftSignup { Id = Guid.NewGuid(), ShiftId = s2.Id, VolunteerId = sipho.Id, SignedUpAt = now },
            new ShiftSignup { Id = Guid.NewGuid(), ShiftId = s2.Id, VolunteerId = emma.Id, SignedUpAt = now },
            new ShiftSignup { Id = Guid.NewGuid(), ShiftId = s3.Id, VolunteerId = emma.Id, SignedUpAt = now });

        // Hour logs (as in the prototype).
        HourLog H(ApplicationUser v, int daysAgo, decimal hours, string activity, HourLogStatus status, string? notes = null) => new()
        {
            Id = Guid.NewGuid(), VolunteerId = v.Id, Date = today.AddDays(-daysAgo), Hours = hours, Activity = activity,
            Status = status, Notes = notes, LoggedAt = now.AddDays(-daysAgo),
            ReviewedById = status == HourLogStatus.Pending ? null : admin.Id,
            ReviewedAt = status == HourLogStatus.Pending ? null : now.AddDays(-daysAgo + 1)
        };
        _db.HourLogs.AddRange(
            H(priya, 2, 3, "Dog walking", HourLogStatus.Approved), H(priya, 9, 4, "Dog walking", HourLogStatus.Approved),
            H(priya, 16, 3.5m, "Dog walking", HourLogStatus.Approved), H(priya, 1, 2, "Dog walking", HourLogStatus.Pending, "Covered an extra shift for Thabo."),
            H(sipho, 3, 5, "Cattery cleaning", HourLogStatus.Approved), H(sipho, 10, 4.5m, "Cattery cleaning", HourLogStatus.Approved),
            H(emma, 4, 6, "Front desk", HourLogStatus.Approved), H(emma, 11, 6, "Front desk", HourLogStatus.Approved),
            H(thabo, 2, 3, "Dog walking", HourLogStatus.Pending), H(thabo, 6, 2.5m, "Dog walking", HourLogStatus.Approved));

        await _db.SaveChangesAsync(ct);
    }
}
