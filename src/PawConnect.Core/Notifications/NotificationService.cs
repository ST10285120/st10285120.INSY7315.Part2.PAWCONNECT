using Microsoft.Extensions.Logging;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;

namespace PawConnect.Core.Notifications;

public class NotificationOptions
{
    public int MaxAttemptsPerChannel { get; set; } = 3;
    public int InitialBackoffMilliseconds { get; set; } = 200;
}

/// <summary>
/// Sends through the selected strategies. Each channel is retried with exponential backoff,
/// then the next channel is tried (risk register: "retry queue with exponential backoff,
/// fallback from SMS to email"). Every attempt is written to the Notifications table.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly NotificationStrategySelector _selector;
    private readonly INotificationRepository _log;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly NotificationOptions _options;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        NotificationStrategySelector selector,
        INotificationRepository log,
        IUnitOfWork uow,
        IClock clock,
        NotificationOptions options,
        ILogger<NotificationService> logger)
    {
        _selector = selector;
        _log = log;
        _uow = uow;
        _clock = clock;
        _options = options;
        _logger = logger;
    }

    public async Task SendAsync(NotificationRequest request, CancellationToken ct = default)
    {
        // Notifications go out after the business change is already saved. If the user closes the
        // page mid-send, finishing the send is still right, so the request's token is not used:
        // the email shouldn't be lost and the user shouldn't see an error for a change that worked.
        ct = CancellationToken.None;
        try
        {
            await SendCoreAsync(request, ct);
        }
        catch (Exception ex)
        {
            // Never let a notification problem (e.g. the log table being unavailable) turn a
            // successful action into an error page.
            _logger.LogError(ex, "Notification '{Subject}' could not be processed", request.Subject);
        }
    }

    private async Task SendCoreAsync(NotificationRequest request, CancellationToken ct)
    {
        var strategies = _selector.SelectFor(request);
        if (strategies.Count == 0)
        {
            _logger.LogWarning("No notification channel can deliver '{Subject}'", request.Subject);
            return;
        }

        foreach (var strategy in strategies)
        {
            var (sent, attempts, error) = await TrySendWithRetryAsync(strategy, request, ct);

            await _log.AddAsync(new NotificationMessage
            {
                Id = Guid.NewGuid(),
                RecipientUserId = request.RecipientUserId,
                Channel = strategy.Channel,
                Recipient = strategy.RecipientFor(request),
                Subject = request.Subject,
                Body = request.Body,
                Urgency = request.Urgency,
                Status = sent ? NotificationStatus.Sent : NotificationStatus.Failed,
                Attempts = attempts,
                Error = error,
                CreatedAt = _clock.UtcNow
            }, ct);
            await _uow.SaveChangesAsync(ct);

            if (sent) return;
            _logger.LogWarning("{Channel} delivery failed for '{Subject}', trying next channel", strategy.Channel, request.Subject);
        }
    }

    private async Task<(bool Sent, int Attempts, string? Error)> TrySendWithRetryAsync(
        INotificationStrategy strategy, NotificationRequest request, CancellationToken ct)
    {
        string? lastError = null;
        var delay = _options.InitialBackoffMilliseconds;

        for (var attempt = 1; attempt <= _options.MaxAttemptsPerChannel; attempt++)
        {
            try
            {
                await strategy.SendAsync(request, ct);
                return (true, attempt, null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastError = ex.Message;
                _logger.LogWarning(ex, "Attempt {Attempt} to send via {Channel} failed", attempt, strategy.Channel);
                if (attempt < _options.MaxAttemptsPerChannel && delay > 0)
                {
                    await Task.Delay(delay, ct);
                    delay *= 2; // exponential backoff
                }
            }
        }

        return (false, _options.MaxAttemptsPerChannel, lastError);
    }
}
