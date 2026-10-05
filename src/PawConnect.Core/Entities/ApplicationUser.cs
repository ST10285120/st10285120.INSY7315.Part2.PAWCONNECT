using Microsoft.AspNetCore.Identity;

namespace PawConnect.Core.Entities;

/// <summary>
/// A PawConnect account. Identity supplies email, phone, password hash and lockout fields.
/// The Adopter / Volunteer / Administrator distinction is modelled as Identity roles
/// (see design class diagram, section 3.2) and enforced in the authorization layer.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";

    /// <summary>Branch a volunteer or administrator works at. Null for adopters.</summary>
    public Guid? BranchId { get; set; }
    public ShelterBranch? Branch { get; set; }

    /// <summary>Main volunteering activity, e.g. "Dog walking". Only used for volunteers.</summary>
    public string? VolunteerRole { get; set; }

    /// <summary>Deactivated accounts cannot sign in (admin "deactivate account" story).</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    /// <summary>POPIA: when the user ticked the privacy-notice consent box.</summary>
    public DateTime? ConsentAcceptedAt { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}
