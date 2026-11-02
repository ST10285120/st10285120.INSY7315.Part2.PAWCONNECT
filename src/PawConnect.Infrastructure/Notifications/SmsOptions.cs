namespace PawConnect.Infrastructure.Notifications;

/// <summary>
/// Settings for the SMS channel (Notifications:Sms in appsettings or App Service settings).
/// The AuthToken is a secret: keep it in Key Vault, never in appsettings.json.
/// </summary>
public class SmsOptions
{
    /// <summary>Turns the SMS channel on. When off, urgent messages go by email only.</summary>
    public bool Enabled { get; set; }

    /// <summary>"Twilio" sends real text messages; "Log" only writes them to the log (development).</summary>
    public string Provider { get; set; } = "Log";

    public string? AccountSid { get; set; }
    public string? AuthToken { get; set; }

    /// <summary>The sending number in E.164 form (e.g. +27...), or leave empty and set MessagingServiceSid.</summary>
    public string? FromNumber { get; set; }
    public string? MessagingServiceSid { get; set; }

    /// <summary>Country calling code used to turn local numbers like 082 123 4567 into +27821234567.</summary>
    public string DefaultCountryCode { get; set; } = "27";

    public bool UsesTwilio => string.Equals(Provider, "Twilio", StringComparison.OrdinalIgnoreCase);

    public bool TwilioIsConfigured =>
        !string.IsNullOrWhiteSpace(AccountSid) && !string.IsNullOrWhiteSpace(AuthToken) &&
        (!string.IsNullOrWhiteSpace(FromNumber) || !string.IsNullOrWhiteSpace(MessagingServiceSid));
}
