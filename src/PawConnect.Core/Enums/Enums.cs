namespace PawConnect.Core.Enums;

public enum Species { Dog, Cat, Other }

public enum AnimalSize { Small, Medium, Large }

/// <summary>Matches the "status" ENUM on the Animals table in the data dictionary.</summary>
public enum AnimalStatus { Available, Pending, Adopted, Fostered }

/// <summary>Matches the "status" ENUM on the AdoptionApplications table (see state diagram, section 4.3).</summary>
public enum ApplicationStatus { Submitted, UnderReview, Approved, Rejected, Adopted, Withdrawn }

public enum MedicalRecordType { Vaccination, Treatment, Checkup, Sterilisation, Other }

public enum DonationType { General, Sponsorship, AdoptionFee }

public enum DonationFrequency { OnceOff, Monthly }

/// <summary>
/// Card payments are out of scope (see Scope in the Task 1 document), so a donation starts as a
/// pledge and a staff member marks it Received once the EFT/cash has cleared.
/// </summary>
public enum DonationStatus { Pledged, Received, Cancelled }

public enum HourLogStatus { Pending, Approved, Rejected }

public enum NotificationChannel { Email, Sms }

public enum NotificationUrgency { Normal, High }

public enum NotificationStatus { Sent, Failed }
