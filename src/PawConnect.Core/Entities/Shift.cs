namespace PawConnect.Core.Entities;

public class Shift
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public ShelterBranch? Branch { get; set; }

    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string Role { get; set; } = "";
    public int Capacity { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// Optimistic concurrency token. Every sign-up changes it, so if two volunteers grab the
    /// last place at the same moment, one save fails and is retried. This stops overbooking.
    /// </summary>
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();

    public List<ShiftSignup> Signups { get; set; } = new();

    public int Filled => Signups.Count;
    public bool IsFull => Filled >= Capacity;
}

public class ShiftSignup
{
    public Guid Id { get; set; }
    public Guid ShiftId { get; set; }
    public Shift? Shift { get; set; }
    public Guid VolunteerId { get; set; }
    public ApplicationUser? Volunteer { get; set; }
    public DateTime SignedUpAt { get; set; }
}
