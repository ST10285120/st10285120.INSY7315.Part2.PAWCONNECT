namespace PawConnect.Infrastructure.Notifications;

/// <summary>
/// Turns the phone numbers people type ("082 123 4567", "+27 82-123-4567", "0027821234567")
/// into the E.164 form SMS providers need ("+27821234567"). Returns null for anything that
/// can't be a valid number, so the SMS channel is skipped and email is used instead.
/// </summary>
public static class PhoneNumberFormatter
{
    public static string? ToE164(string? input, string defaultCountryCode = "27")
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var trimmed = input.Trim();
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return null;

        string international;
        if (trimmed.StartsWith('+')) international = digits;
        else if (digits.StartsWith("00")) international = digits[2..];
        else if (digits.StartsWith('0')) international = defaultCountryCode + digits[1..];
        else international = digits;

        // E.164 allows at most 15 digits; anything under 8 is too short to be a real mobile number.
        return international.Length is >= 8 and <= 15 && international[0] != '0' ? "+" + international : null;
    }
}
