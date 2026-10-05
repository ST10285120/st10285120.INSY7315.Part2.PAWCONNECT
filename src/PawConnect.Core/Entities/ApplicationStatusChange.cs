using PawConnect.Core.Enums;

namespace PawConnect.Core.Entities;

/// <summary>Audit trail of every status transition on an adoption application.</summary>
public class ApplicationStatusChange
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public ApplicationStatus? FromStatus { get; set; }
    public ApplicationStatus ToStatus { get; set; }
    public Guid? ChangedById { get; set; }
    public ApplicationUser? ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; }
    public string? Note { get; set; }
}
