namespace PawConnect.Core.Common;

/// <summary>
/// All timestamps are stored in UTC (PostgreSQL timestamptz). The shelter works in
/// South African Standard Time, so this helper converts for display and input.
/// </summary>
public static class ShelterTime
{
    private static readonly TimeZoneInfo Zone = FindZone();

    private static TimeZoneInfo FindZone()
    {
        foreach (var id in new[] { "Africa/Johannesburg", "South Africa Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        // SAST has no daylight saving, so a fixed +02:00 offset is an exact fallback.
        return TimeZoneInfo.CreateCustomTimeZone("SAST", TimeSpan.FromHours(2), "SAST", "SAST");
    }

    public static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    public static DateTime ToUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone);

    public static DateOnly Today(IClock clock) => DateOnly.FromDateTime(ToLocal(clock.UtcNow));
}
