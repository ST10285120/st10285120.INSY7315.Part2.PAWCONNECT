using Microsoft.Extensions.Logging.Abstractions;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Notifications;
using PawConnect.Core.Receipts;
using PawConnect.Core.Services;
using PawConnect.Tests.Support;

namespace PawConnect.Tests.Unit;

/// <summary>State machine from the design doc's state diagram (section 4.3).</summary>
public class ApplicationStateMachineTests
{
    [Theory]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.UnderReview)]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Withdrawn)]
    [InlineData(ApplicationStatus.UnderReview, ApplicationStatus.Approved)]
    [InlineData(ApplicationStatus.UnderReview, ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Approved, ApplicationStatus.Adopted)]
    [InlineData(ApplicationStatus.Approved, ApplicationStatus.Withdrawn)]
    public void Allows_valid_transitions(ApplicationStatus from, ApplicationStatus to) =>
        Assert.True(ApplicationStateMachine.CanTransition(from, to));

    [Theory]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Approved)]   // must be reviewed first
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Adopted)]
    [InlineData(ApplicationStatus.Rejected, ApplicationStatus.Adopted)]     // the doc's key example
    [InlineData(ApplicationStatus.Rejected, ApplicationStatus.Approved)]
    [InlineData(ApplicationStatus.Withdrawn, ApplicationStatus.Submitted)]
    [InlineData(ApplicationStatus.Adopted, ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.UnderReview, ApplicationStatus.Adopted)]
    public void Blocks_invalid_transitions(ApplicationStatus from, ApplicationStatus to) =>
        Assert.False(ApplicationStateMachine.CanTransition(from, to));

    [Theory]
    [InlineData(ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Adopted)]
    [InlineData(ApplicationStatus.Withdrawn)]
    public void Final_states_are_terminal(ApplicationStatus status) => Assert.True(ApplicationStateMachine.IsTerminal(status));

    [Fact]
    public void Under_review_has_a_friendly_label() => Assert.Equal("Under Review", ApplicationStatus.UnderReview.Label());
}

/// <summary>Strategy pattern: channel selection by urgency and availability.</summary>
public class NotificationStrategyTests
{
    private sealed class FakeStrategy : INotificationStrategy
    {
        public FakeStrategy(NotificationChannel channel, int failuresBeforeSuccess = 0) { Channel = channel; _failures = failuresBeforeSuccess; }
        private int _failures;
        public int Calls { get; private set; }
        public NotificationChannel Channel { get; }
        public bool CanSend(NotificationRequest r) => Channel == NotificationChannel.Email ? r.Email != null : r.Phone != null;
        public string RecipientFor(NotificationRequest r) => (Channel == NotificationChannel.Email ? r.Email : r.Phone) ?? "";
        public Task SendAsync(NotificationRequest r, CancellationToken ct = default)
        {
            Calls++;
            if (_failures-- > 0) throw new InvalidOperationException("provider down");
            return Task.CompletedTask;
        }
    }

    private sealed class ListNotificationRepo : INotificationRepository
    {
        public List<NotificationMessage> Items { get; } = new();
        public Task AddAsync(NotificationMessage m, CancellationToken ct = default) { Items.Add(m); return Task.CompletedTask; }
        public Task<List<NotificationMessage>> GetRecentAsync(int count, CancellationToken ct = default) => Task.FromResult(Items);
    }

    private sealed class NoopUow : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
        public void DiscardChanges() { }
    }

    [Fact]
    public void Normal_messages_only_use_email_to_keep_costs_down()
    {
        var selector = new NotificationStrategySelector(new[] { new FakeStrategy(NotificationChannel.Sms), new FakeStrategy(NotificationChannel.Email) });
        var chosen = selector.SelectFor(new NotificationRequest("s", "b", "a@b.c", "082"));
        Assert.Single(chosen);
        Assert.Equal(NotificationChannel.Email, chosen[0].Channel);
    }

    [Fact]
    public void Urgent_messages_prefer_sms_with_email_fallback()
    {
        var selector = new NotificationStrategySelector(new[] { new FakeStrategy(NotificationChannel.Email), new FakeStrategy(NotificationChannel.Sms) });
        var chosen = selector.SelectFor(new NotificationRequest("s", "b", "a@b.c", "082", Urgency: NotificationUrgency.High));
        Assert.Equal(new[] { NotificationChannel.Sms, NotificationChannel.Email }, chosen.Select(c => c.Channel).ToArray());
    }

    [Fact]
    public void Channels_without_the_needed_contact_detail_are_skipped()
    {
        var selector = new NotificationStrategySelector(new[] { new FakeStrategy(NotificationChannel.Sms), new FakeStrategy(NotificationChannel.Email) });
        var chosen = selector.SelectFor(new NotificationRequest("s", "b", "a@b.c", Phone: null, Urgency: NotificationUrgency.High));
        Assert.Equal(NotificationChannel.Email, Assert.Single(chosen).Channel);
    }

    [Fact]
    public async Task Retries_with_backoff_then_succeeds_and_logs_once()
    {
        var email = new FakeStrategy(NotificationChannel.Email, failuresBeforeSuccess: 2);
        var repo = new ListNotificationRepo();
        var service = new NotificationService(new NotificationStrategySelector(new[] { email }), repo, new NoopUow(), new FakeClock(),
            new NotificationOptions { MaxAttemptsPerChannel = 3, InitialBackoffMilliseconds = 0 }, NullLogger<NotificationService>.Instance);

        await service.SendAsync(new NotificationRequest("Hi", "Body", "a@b.c"));

        Assert.Equal(3, email.Calls);
        var logged = Assert.Single(repo.Items);
        Assert.Equal(NotificationStatus.Sent, logged.Status);
        Assert.Equal(3, logged.Attempts);
    }

    [Fact]
    public async Task Falls_back_from_sms_to_email_when_sms_keeps_failing()
    {
        var sms = new FakeStrategy(NotificationChannel.Sms, failuresBeforeSuccess: 99);
        var email = new FakeStrategy(NotificationChannel.Email);
        var repo = new ListNotificationRepo();
        var service = new NotificationService(new NotificationStrategySelector(new INotificationStrategy[] { sms, email }), repo, new NoopUow(), new FakeClock(),
            new NotificationOptions { MaxAttemptsPerChannel = 2, InitialBackoffMilliseconds = 0 }, NullLogger<NotificationService>.Instance);

        await service.SendAsync(new NotificationRequest("Shift", "Body", "a@b.c", "082", Urgency: NotificationUrgency.High));

        Assert.Equal(2, sms.Calls);
        Assert.Equal(1, email.Calls);
        Assert.Equal(new[] { NotificationStatus.Failed, NotificationStatus.Sent }, repo.Items.Select(i => i.Status).ToArray());
    }

    [Fact]
    public async Task Never_throws_even_if_every_channel_fails()
    {
        var email = new FakeStrategy(NotificationChannel.Email, failuresBeforeSuccess: 99);
        var service = new NotificationService(new NotificationStrategySelector(new[] { email }), new ListNotificationRepo(), new NoopUow(), new FakeClock(),
            new NotificationOptions { MaxAttemptsPerChannel = 1, InitialBackoffMilliseconds = 0 }, NullLogger<NotificationService>.Instance);
        await service.SendAsync(new NotificationRequest("x", "y", "a@b.c")); // no exception
    }
}

/// <summary>Factory pattern: receipts built consistently from donations.</summary>
public class DonationReceiptFactoryTests
{
    private readonly DonationReceiptFactory _factory = new();
    private static Donation D(DonationType type, DonationFrequency freq = DonationFrequency.OnceOff, DonationStatus status = DonationStatus.Pledged) => new()
    {
        Id = Guid.NewGuid(), Amount = 1250m, Type = type, Frequency = freq, Status = status, ReceiptNumber = "RCPT-1",
        DonatedAt = new DateTime(2026, 9, 26, 8, 0, 0, DateTimeKind.Utc), ReceivedAt = status == DonationStatus.Received ? new DateTime(2026, 9, 27, 8, 0, 0, DateTimeKind.Utc) : null
    };

    [Fact]
    public void Receipt_numbers_use_the_shelter_date_and_an_unambiguous_suffix()
    {
        var number = _factory.NewReceiptNumber(new DateTime(2026, 9, 26, 23, 30, 0, DateTimeKind.Utc)); // 01:30 on the 27th in SAST
        Assert.Matches(@"^RCPT-20260927-[A-HJ-NP-Z2-9]{6}$", number);
    }

    [Theory]
    [InlineData(DonationType.General, "General donation")]
    [InlineData(DonationType.Sponsorship, "Sponsorship of Biscuit's care")]
    [InlineData(DonationType.AdoptionFee, "Adoption fee for Biscuit")]
    public void Describes_each_donation_type(DonationType type, string expected) =>
        Assert.StartsWith(expected, _factory.Create(D(type), "Jordan Adams", "Biscuit").Description);

    [Fact]
    public void Monthly_donations_are_labelled_and_amount_is_in_rand()
    {
        var receipt = _factory.Create(D(DonationType.General, DonationFrequency.Monthly), "Jordan", null);
        Assert.EndsWith("(monthly)", receipt.Description);
        Assert.Equal("R1,250.00", receipt.AmountDisplay);
    }

    [Fact]
    public void Status_line_reflects_pledged_or_received()
    {
        Assert.Contains("Pledged", _factory.Create(D(DonationType.General), "J", null).StatusLine);
        Assert.Contains("Received on 27 September 2026", _factory.Create(D(DonationType.General, status: DonationStatus.Received), "J", null).StatusLine);
    }

    [Fact]
    public void Plain_text_contains_all_key_fields()
    {
        var text = _factory.Create(D(DonationType.Sponsorship), "Jordan Adams", "Biscuit").ToPlainText();
        Assert.Contains("RCPT-1", text);
        Assert.Contains("Jordan Adams", text);
        Assert.Contains("R1,250.00", text);
    }
}

public class HelperTests
{
    [Theory]
    [InlineData("=SUM(A1)", "\"'=SUM(A1)\"")]
    [InlineData("+27 82", "\"'+27 82\"")]
    [InlineData("@evil", "\"'@evil\"")]
    [InlineData("Say \"hi\"", "\"Say \"\"hi\"\"\"")]
    [InlineData("Plain", "\"Plain\"")]
    public void Csv_fields_are_quoted_and_formula_injection_is_neutralised(string input, string expected) =>
        Assert.Equal(expected, ReportService.Csv(input));

    [Fact]
    public void Local_date_range_converts_to_utc_using_sast()
    {
        var (from, to) = ReportService.ToUtcRange(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));
        Assert.Equal(new DateTime(2026, 8, 31, 22, 0, 0), from);
        Assert.Equal(new DateTime(2026, 9, 30, 22, 0, 0), to);
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 }, "image/png")]
    [InlineData(new byte[] { (byte)'<', (byte)'h', (byte)'t', (byte)'m', (byte)'l' }, null)]
    public void Detects_image_type_from_file_bytes(byte[] bytes, string? expected) =>
        Assert.Equal(expected, AnimalService.DetectImageType(bytes));

    [Theory]
    [InlineData(6, 0, "6 mo")]
    [InlineData(11, 2, "1 yr")]
    [InlineData(24, 1, "2 yrs")]
    [InlineData(1, 0, "1 mo")]
    public void Age_label_accounts_for_time_in_care(int ageAtIntake, int monthsLater, string expected)
    {
        var animal = new Animal { AgeMonthsAtIntake = ageAtIntake, IntakeDate = new DateOnly(2026, 1, 15) };
        Assert.Equal(expected, animal.AgeLabel(new DateOnly(2026, 1 + monthsLater, 20)));
    }

    [Fact]
    public void Application_references_look_right() =>
        Assert.Matches(@"^PC-260926-[A-HJ-NP-Z2-9]{4}$", ReferenceGenerator.NewApplicationReference(new DateTime(2026, 9, 26, 8, 0, 0, DateTimeKind.Utc)));
}

public class ConnectionStringTests
{
    [Fact]
    public void Neon_style_urls_are_converted_to_npgsql_format()
    {
        var result = PawConnect.Infrastructure.Data.PostgresConnectionString.Normalize(
            "postgresql://paw_owner:p%40ss@ep-cool-sun-123456.eu-central-1.aws.neon.tech/pawconnect?sslmode=require&channel_binding=require");
        var b = new Npgsql.NpgsqlConnectionStringBuilder(result);
        Assert.Equal("ep-cool-sun-123456.eu-central-1.aws.neon.tech", b.Host);
        Assert.Equal(5432, b.Port);
        Assert.Equal("pawconnect", b.Database);
        Assert.Equal("paw_owner", b.Username);
        Assert.Equal("p@ss", b.Password);
        Assert.Equal(Npgsql.SslMode.Require, b.SslMode);
    }

    [Fact]
    public void Normal_connection_strings_are_left_alone()
    {
        const string cs = "Host=localhost;Port=5432;Database=pawconnect;Username=pawconnect;Password=pawconnect";
        Assert.Equal(cs, PawConnect.Infrastructure.Data.PostgresConnectionString.Normalize(cs));
    }
}
