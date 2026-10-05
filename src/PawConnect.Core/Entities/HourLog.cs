using PawConnect.Core.Enums;

namespace PawConnect.Core.Entities;

public class HourLog
{
    public Guid Id { get; set; }
    public Guid VolunteerId { get; set; }
    public ApplicationUser? Volunteer { get; set; }

    public DateOnly Date { get; set; }
    public decimal Hours { get; set; }
    public string Activity { get; set; } = "";
    public string? Notes { get; set; }

    public HourLogStatus Status { get; set; } = HourLogStatus.Pending;
    public DateTime LoggedAt { get; set; }
    public Guid? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
