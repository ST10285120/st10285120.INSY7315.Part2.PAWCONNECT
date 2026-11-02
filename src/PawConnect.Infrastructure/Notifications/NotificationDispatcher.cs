using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PawConnect.Core.Notifications;

namespace PawConnect.Infrastructure.Notifications;

/// <summary>
/// Hosted background worker: takes queued notifications one at a time and sends them through
/// <see cref="NotificationService"/> (strategies, retry with backoff, fallback, logging) in its own
/// DI scope, so it uses its own database context, never a request's. On shutdown it gets a few
/// seconds to finish what is already queued.
/// </summary>
public sealed class NotificationDispatcher : BackgroundService
{
    private readonly BackgroundNotificationQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(BackgroundNotificationQueue queue, IServiceScopeFactory scopes, ILogger<NotificationDispatcher> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var request in _queue.Reader.ReadAllAsync(stoppingToken))
                await DispatchAsync(request);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down: send what is already queued, for at most 5 seconds.
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline && _queue.Reader.TryRead(out var pending))
                await DispatchAsync(pending);
        }
    }

    private async Task DispatchAsync(NotificationRequest request)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<NotificationService>().SendAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background notification '{Subject}' failed", request.Subject);
        }
    }
}
