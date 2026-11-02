using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using PawConnect.Core.Enums;
using PawConnect.Core.Notifications;

namespace PawConnect.Infrastructure.Notifications;

/// <summary>
/// Sends real text messages through Twilio's REST API (no SDK needed: one form POST).
/// Twilio was chosen over Azure Communication Services because ACS cannot send SMS to South
/// African numbers, and Twilio's pay-as-you-go pricing fits the plan of using SMS only for urgent
/// shift messages (design doc, section 9). A failure throws, so NotificationService retries with
/// backoff and then falls back to email, exactly like the email channel.
/// </summary>
public class TwilioSmsStrategy : INotificationStrategy
{
    public const string HttpClientName = "twilio";
    private const int MaxBodyLength = 480; // three SMS segments: keeps the cost of a message predictable

    private readonly SmsOptions _options;
    private readonly IHttpClientFactory _httpClients;
    private readonly ILogger<TwilioSmsStrategy> _logger;

    public TwilioSmsStrategy(SmsOptions options, IHttpClientFactory httpClients, ILogger<TwilioSmsStrategy> logger)
    {
        _options = options;
        _httpClients = httpClients;
        _logger = logger;
    }

    public NotificationChannel Channel => NotificationChannel.Sms;

    public bool CanSend(NotificationRequest request) =>
        _options.TwilioIsConfigured && PhoneNumberFormatter.ToE164(request.Phone, _options.DefaultCountryCode) != null;

    public string RecipientFor(NotificationRequest request) =>
        PhoneNumberFormatter.ToE164(request.Phone, _options.DefaultCountryCode) ?? request.Phone ?? "";

    public async Task SendAsync(NotificationRequest request, CancellationToken ct = default)
    {
        var to = PhoneNumberFormatter.ToE164(request.Phone, _options.DefaultCountryCode)
                 ?? throw new InvalidOperationException("The recipient has no valid mobile number.");

        var form = new Dictionary<string, string> { ["To"] = to, ["Body"] = BuildText(request) };
        if (!string.IsNullOrWhiteSpace(_options.MessagingServiceSid)) form["MessagingServiceSid"] = _options.MessagingServiceSid!;
        else form["From"] = _options.FromNumber!;

        using var message = new HttpRequestMessage(HttpMethod.Post,
            $"2010-04-01/Accounts/{Uri.EscapeDataString(_options.AccountSid!)}/Messages.json")
        {
            Content = new FormUrlEncodedContent(form)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_options.AccountSid}:{_options.AuthToken}")));

        var client = _httpClients.CreateClient(HttpClientName);
        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(message, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // HttpClient reports its own timeout as a cancellation; make it a normal failure so it is retried.
            throw new TimeoutException("The SMS provider did not respond within 10 seconds.");
        }
        using var _ = response;
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(ct);
            // Throwing lets NotificationService retry and then fall back to email.
            throw new HttpRequestException(
                $"Twilio rejected the SMS ({(int)response.StatusCode}): {Truncate(detail, 300)}", null, response.StatusCode);
        }
        _logger.LogInformation("SMS sent to {Phone} | {Subject}", Mask(to), request.Subject);
    }

    /// <summary>A text message has no subject line, so the subject leads the text.</summary>
    public static string BuildText(NotificationRequest request)
    {
        var text = $"PawConnect: {request.Subject}. {request.Body}".Replace("\r", " ").Replace("\n", " ");
        while (text.Contains("  ")) text = text.Replace("  ", " ");
        return Truncate(text.Trim(), MaxBodyLength);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

    /// <summary>Logs only the last three digits (POPIA data minimisation).</summary>
    private static string Mask(string phone) => phone.Length <= 3 ? "***" : new string('*', phone.Length - 3) + phone[^3..];
}
