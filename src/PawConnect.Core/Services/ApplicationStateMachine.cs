using PawConnect.Core.Enums;

namespace PawConnect.Core.Services;

/// <summary>
/// The adoption application lifecycle from the state diagram (design doc, section 4.3).
/// Only the transitions listed here are allowed, so a rejected application can never later
/// be marked adopted, and Approved can only be reached from Under Review.
/// </summary>
public static class ApplicationStateMachine
{
    private static readonly Dictionary<ApplicationStatus, ApplicationStatus[]> Allowed = new()
    {
        [ApplicationStatus.Submitted] = new[] { ApplicationStatus.UnderReview, ApplicationStatus.Rejected, ApplicationStatus.Withdrawn },
        [ApplicationStatus.UnderReview] = new[] { ApplicationStatus.Approved, ApplicationStatus.Rejected, ApplicationStatus.Withdrawn },
        [ApplicationStatus.Approved] = new[] { ApplicationStatus.Adopted, ApplicationStatus.Rejected, ApplicationStatus.Withdrawn },
        // Terminal states: nothing can follow them.
        [ApplicationStatus.Rejected] = Array.Empty<ApplicationStatus>(),
        [ApplicationStatus.Adopted] = Array.Empty<ApplicationStatus>(),
        [ApplicationStatus.Withdrawn] = Array.Empty<ApplicationStatus>(),
    };

    public static bool CanTransition(ApplicationStatus from, ApplicationStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static IReadOnlyList<ApplicationStatus> NextStates(ApplicationStatus from) =>
        Allowed.TryGetValue(from, out var targets) ? targets : Array.Empty<ApplicationStatus>();

    public static bool IsTerminal(ApplicationStatus status) => NextStates(status).Count == 0;

    public static string Label(this ApplicationStatus status) => status switch
    {
        ApplicationStatus.UnderReview => "Under Review",
        _ => status.ToString()
    };
}
