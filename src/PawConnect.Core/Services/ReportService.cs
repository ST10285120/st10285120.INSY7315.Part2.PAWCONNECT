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

public record MonthlyTotal(string Label, int Year, int Month, decimal Amount);

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

/// <summary>Numbers for the shelter dashboard and the funder report / CSV export.</summary>
public class ReportService
{
    private readonly IAnimalRepository _animals;
    private readonly IApplicationRepository _applications;
    private readonly IDonationRepository _donations;
    private readonly IHourLogRepository _hours;
    private readonly IUserRepository _users;
    private readonly IClock _clock;

    public ReportService(IAnimalRepository animals, IApplicationRepository applications, IDonationRepository donations,
        IHourLogRepository hours, IUserRepository users, IClock clock)
    {
        _animals = animals;
        _applications = applications;
        _donations = donations;
        _hours = hours;
        _users = users;
        _clock = clock;
    }

    public async Task<DashboardStats> GetDashboardAsync(CancellationToken ct = default)
    {
        var today = ShelterTime.Today(_clock);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var (fromUtc, toUtc) = ToUtcRange(monthStart, today);

        var volunteers = await _users.GetInRoleAsync(Roles.Volunteer, ct);
        var received = await _donations.GetBetweenAsync(fromUtc, toUtc, DonationStatus.Received, ct);

        return new DashboardStats(
            AnimalsInCare: await _animals.CountInCareAsync(ct),
            AdoptionsThisMonth: await _applications.CountAdoptedBetweenAsync(fromUtc, toUtc, ct),
            ActiveVolunteers: volunteers.Count(v => v.IsActive),
            VolunteerHoursThisMonth: await _hours.SumApprovedAsync(null, monthStart, today, ct),
            DonationsReceivedThisMonth: received.Sum(d => d.Amount),
            OpenApplications: (await _applications.GetOpenForReviewAsync(null, ct)).Count,
            PendingHourLogs: await _hours.CountPendingAsync(null, ct),
            PledgesAwaitingPayment: (await _donations.GetPledgedAsync(ct)).Count);
    }

    /// <summary>Received donations per calendar month (shelter time) for the last <paramref name="months"/> months.</summary>
    public async Task<List<MonthlyTotal>> GetMonthlyDonationsAsync(int months, CancellationToken ct = default)
    {
        var today = ShelterTime.Today(_clock);
        var first = new DateOnly(today.Year, today.Month, 1).AddMonths(-(months - 1));
        var (fromUtc, toUtc) = ToUtcRange(first, today);
        var donations = await _donations.GetBetweenAsync(fromUtc, toUtc, DonationStatus.Received, ct);

        var byMonth = donations
            .GroupBy(d => { var local = ShelterTime.ToLocal(d.ReceivedAt ?? d.DonatedAt); return (local.Year, local.Month); })
            .ToDictionary(g => g.Key, g => g.Sum(d => d.Amount));

        return Enumerable.Range(0, months)
            .Select(i => first.AddMonths(i))
            .Select(m => new MonthlyTotal(
                m.ToString("MMM", CultureInfo.InvariantCulture), m.Year, m.Month,
                byMonth.TryGetValue((m.Year, m.Month), out var total) ? total : 0m))
            .ToList();
    }

    public async Task<OperationResult<ReportSummary>> GetSummaryAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (to < from) return OperationResult<ReportSummary>.Failure("The end date must be on or after the start date.");
        if (to.DayNumber - from.DayNumber > 366 * 5) return OperationResult<ReportSummary>.Failure("Please choose a range of at most five years.");

        var (fromUtc, toUtc) = ToUtcRange(from, to);
        var all = await _donations.GetBetweenAsync(fromUtc, toUtc, null, ct);
        var received = all.Where(d => d.Status == DonationStatus.Received).ToList();

        return OperationResult<ReportSummary>.Success(new ReportSummary(
            from, to,
            DonationsReceived: received.Sum(d => d.Amount),
            DonationCount: received.Count,
            GeneralDonations: received.Where(d => d.Type == DonationType.General).Sum(d => d.Amount),
            Sponsorships: received.Where(d => d.Type == DonationType.Sponsorship).Sum(d => d.Amount),
            AdoptionFees: received.Where(d => d.Type == DonationType.AdoptionFee).Sum(d => d.Amount),
            OutstandingPledges: all.Where(d => d.Status == DonationStatus.Pledged).Sum(d => d.Amount),
            ApplicationsSubmitted: await _applications.CountSubmittedBetweenAsync(fromUtc, toUtc, ct),
            Adoptions: await _applications.CountAdoptedBetweenAsync(fromUtc, toUtc, ct),
            AverageDecisionHours: await _applications.AverageDecisionHoursAsync(fromUtc, toUtc, ct),
            VolunteerHours: await _hours.SumApprovedAsync(null, from, to, ct),
            Donations: all.OrderBy(d => d.DonatedAt).ToList()));
    }

    /// <summary>Donations report as CSV for funding applications (administrator story).</summary>
    public static string ToCsv(IEnumerable<Donation> donations)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Receipt number,Date (SAST),Donor,Type,Animal,Frequency,Status,Amount (ZAR)");
        foreach (var d in donations)
        {
            sb.AppendLine(string.Join(",",
                Csv(d.ReceiptNumber),
                Csv(ShelterTime.ToLocal(d.DonatedAt).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)),
                Csv(d.Donor?.FullName ?? ""),
                Csv(d.Type.ToString()),
                Csv(d.Animal?.Name ?? ""),
                Csv(d.Frequency.ToString()),
                Csv(d.Status.ToString()),
                d.Amount.ToString("0.00", CultureInfo.InvariantCulture)));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Quotes a CSV field and neutralises spreadsheet formulas (CSV injection): values starting
    /// with = + - @ are prefixed with an apostrophe so Excel treats them as text.
    /// </summary>
    public static string Csv(string value)
    {
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0])) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>Converts an inclusive local date range to a half-open UTC range [from, to).</summary>
    public static (DateTime FromUtc, DateTime ToUtc) ToUtcRange(DateOnly from, DateOnly to) =>
        (ShelterTime.ToUtc(from.ToDateTime(TimeOnly.MinValue)), ShelterTime.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue)));
}
