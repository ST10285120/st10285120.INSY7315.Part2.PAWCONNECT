using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Core.Interfaces;

/// <summary>Raised by the unit of work when an optimistic concurrency check fails.</summary>
public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message, Exception? inner = null) : base(message, inner) { }
}
