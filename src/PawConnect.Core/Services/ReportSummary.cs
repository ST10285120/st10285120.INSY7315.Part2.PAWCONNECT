using System.Globalization;
using System.Text;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;

namespace PawConnect.Core.Services;

public record ReportSummary(
    DateOnly From,
    DateOnly To,
    decimal DonationsReceived,
    int DonationCount,
    decimal GeneralDonations,
    decimal Sponsorships,
    decimal AdoptionFees,
    decimal OutstandingPledges,
    int ApplicationsSubmitted,
    int Adoptions,
    double? AverageDecisionHours,
    decimal VolunteerHours,
    IReadOnlyList<Donation> Donations);
