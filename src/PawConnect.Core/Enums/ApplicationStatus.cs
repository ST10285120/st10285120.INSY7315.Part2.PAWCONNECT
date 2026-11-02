namespace PawConnect.Core.Enums;

/// <summary>Matches the "status" ENUM on the AdoptionApplications table (see state diagram, section 4.3).</summary>
public enum ApplicationStatus { Submitted, UnderReview, Approved, Rejected, Adopted, Withdrawn }
