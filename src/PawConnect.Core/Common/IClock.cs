namespace PawConnect.Core.Common;

/// <summary>Abstraction over the system clock so time-based rules can be unit tested.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
