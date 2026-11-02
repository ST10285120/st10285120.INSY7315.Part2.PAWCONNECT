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
