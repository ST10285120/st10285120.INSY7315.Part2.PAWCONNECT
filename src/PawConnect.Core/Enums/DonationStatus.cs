namespace PawConnect.Core.Enums;

/// <summary>
/// Card payments are out of scope (see Scope in the Task 1 document), so a donation starts as a
/// pledge and a staff member marks it Received once the EFT/cash has cleared.
/// </summary>
public enum DonationStatus { Pledged, Received, Cancelled }
