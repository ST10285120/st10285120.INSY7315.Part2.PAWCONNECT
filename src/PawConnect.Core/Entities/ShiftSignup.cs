namespace PawConnect.Core.Entities;

public class ShiftSignup
{
    public Guid Id { get; set; }
    public Guid ShiftId { get; set; }
    public Shift? Shift { get; set; }
    public Guid VolunteerId { get; set; }
    public ApplicationUser? Volunteer { get; set; }
    public DateTime SignedUpAt { get; set; }
}
