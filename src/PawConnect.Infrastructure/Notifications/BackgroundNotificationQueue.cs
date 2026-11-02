using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PawConnect.Core.Notifications;

namespace PawConnect.Infrastructure.Notifications;

/// <summary>
/// What the business services call: it only puts the message on an in-process queue and returns
/// at once, so a slow or failing mail server never delays the user's request (the change it
/// reports is already saved). <see cref="NotificationDispatcher"/> sends it in the background.
/// </summary>
public sealed class BackgroundNotificationQueue : INotificationService
{
    private readonly Channel<NotificationRequest> _channel =
        Channel.CreateBounded<NotificationRequest>(new BoundedChannelOptions(1000) { SingleReader = true });
    private readonly ILogger<BackgroundNotificationQueue> _logger;

    public BackgroundNotificationQueue(ILogger<BackgroundNotificationQueue> logger) => _logger = logger;

    public ChannelReader<NotificationRequest> Reader => _channel.Reader;

    public Task SendAsync(NotificationRequest request, CancellationToken ct = default)
    {
        if (!_channel.Writer.TryWrite(request))
            _logger.LogError("Notification queue is full; '{Subject}' was not sent", request.Subject);
        return Task.CompletedTask;
    }
}
