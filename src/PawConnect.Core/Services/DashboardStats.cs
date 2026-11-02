using System.Globalization;
using System.Text;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;

namespace PawConnect.Core.Services;

public record DashboardStats(
    int AnimalsInCare,
    int AdoptionsThisMonth,
    int ActiveVolunteers,
    decimal VolunteerHoursThisMonth,
    decimal DonationsReceivedThisMonth,
    int OpenApplications,
    int PendingHourLogs,
    int PledgesAwaitingPayment);
