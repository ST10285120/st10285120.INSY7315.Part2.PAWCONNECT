using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using PawConnect.Core.Enums;
using PawConnect.Core.Notifications;
using PawConnect.Infrastructure.Notifications;

namespace PawConnect.Tests.Unit;

/// <summary>The real SMS channel (Task 2 feedback: replace the placeholder SMS sender).</summary>
public class SmsTests
{
    [Theory]
    [InlineData("082 123 4567", "+27821234567")]
    [InlineData("+27 82-123-4567", "+27821234567")]
    [InlineData("0027821234567", "+27821234567")]
    [InlineData("27821234567", "+27821234567")]
    [InlineData("+44 7700 900123", "+447700900123")]
    public void Local_and_international_numbers_become_e164(string input, string expected) =>
        Assert.Equal(expected, PhoneNumberFormatter.ToE164(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("call me")]
    [InlineData("12345")]
    [InlineData("+1234567890123456")]
    public void Numbers_that_cannot_be_texted_are_rejected(string? input) =>
        Assert.Null(PhoneNumberFormatter.ToE164(input));

    [Fact]
    public async Task Sends_a_form_post_to_twilio_with_basic_auth()
    {
        var handler = new RecordingHandler(HttpStatusCode.Created, "{\"sid\":\"SM1\"}");
        var sms = Strategy(handler);

        await sms.SendAsync(Request("082 123 4567"));

        var sent = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal("https://api.twilio.com/2010-04-01/Accounts/AC123/Messages.json", sent.Uri);
        Assert.Equal("Basic", sent.AuthScheme);
        Assert.Contains("To=%2B27821234567", sent.Body);
        Assert.Contains("From=%2B15005550006", sent.Body);
        Assert.Contains("Body=PawConnect", sent.Body);
    }

    [Fact]
    public async Task A_rejected_message_throws_so_the_service_falls_back_to_email()
    {
        var sms = Strategy(new RecordingHandler(HttpStatusCode.BadRequest, "{\"message\":\"invalid number\"}"));
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => sms.SendAsync(Request("082 123 4567")));
        Assert.Contains("invalid number", ex.Message);
    }

    [Fact]
    public void Only_sends_when_configured_and_the_number_is_valid()
    {
        var sms = Strategy(new RecordingHandler(HttpStatusCode.Created, "{}"));
        Assert.True(sms.CanSend(Request("082 123 4567")));
        Assert.False(sms.CanSend(Request(null)));
        Assert.False(sms.CanSend(Request("not a number")));

        var unconfigured = new TwilioSmsStrategy(new SmsOptions { Enabled = true, Provider = "Twilio" },
            new StubFactory(new RecordingHandler(HttpStatusCode.Created, "{}")), NullLogger<TwilioSmsStrategy>.Instance);
        Assert.False(unconfigured.CanSend(Request("082 123 4567")));
        Assert.Equal(NotificationChannel.Sms, sms.Channel);
    }

    [Fact]
    public void Text_puts_the_subject_first_and_stays_within_three_segments()
    {
        var text = TwilioSmsStrategy.BuildText(new NotificationRequest("Shift tomorrow", new string('x', 1000) + "\n\nend", null));
        Assert.StartsWith("PawConnect: Shift tomorrow. ", text);
        Assert.True(text.Length <= 480);
        Assert.DoesNotContain("\n", text);
    }

    private static NotificationRequest Request(string? phone) =>
        new("Shift reminder", "Dog walking at 08:00.", "v@example.com", phone, null, NotificationUrgency.High);

    private static TwilioSmsStrategy Strategy(RecordingHandler handler) => new(
        new SmsOptions { Enabled = true, Provider = "Twilio", AccountSid = "AC123", AuthToken = "secret", FromNumber = "+15005550006" },
        new StubFactory(handler), NullLogger<TwilioSmsStrategy>.Instance);

    private sealed class StubFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public StubFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) =>
            new(_handler, disposeHandler: false) { BaseAddress = new Uri("https://api.twilio.com/") };
    }

    private sealed record SentRequest(HttpMethod Method, string Uri, string? AuthScheme, string Body);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        public List<SentRequest> Requests { get; } = new();
        public RecordingHandler(HttpStatusCode status, string body) { _status = status; _body = body; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(new SentRequest(request.Method, request.RequestUri!.ToString(),
                request.Headers.Authorization?.Scheme, await request.Content!.ReadAsStringAsync(ct)));
            return new HttpResponseMessage(_status) { Content = new StringContent(_body) };
        }
    }
}
