namespace PawConnect.Core.Common;

/// <summary>Why a business operation failed. The API maps these to HTTP status codes.</summary>
public enum OperationErrorKind
{
    None,
    /// <summary>The input was invalid (HTTP 400).</summary>
    Validation,
    /// <summary>The record doesn't exist, or the caller isn't allowed to know it does (HTTP 404).</summary>
    NotFound,
    /// <summary>The request was valid but clashes with the record's current state (HTTP 409).</summary>
    Conflict
}

/// <summary>
/// Result of a business operation. Services return this instead of throwing for expected
/// failures (e.g. "shift is full") so controllers can show a friendly message, and the API can
/// return the right status code from <see cref="ErrorKind"/>.
/// </summary>
public class OperationResult
{
    public bool Succeeded { get; protected init; }
    public string? Error { get; protected init; }
    public OperationErrorKind ErrorKind { get; protected init; }

    public static OperationResult Success() => new() { Succeeded = true };
    public static OperationResult Failure(string error, OperationErrorKind kind = OperationErrorKind.Validation) =>
        new() { Succeeded = false, Error = error, ErrorKind = kind };
    public static OperationResult NotFound(string error) => Failure(error, OperationErrorKind.NotFound);
    public static OperationResult Conflict(string error) => Failure(error, OperationErrorKind.Conflict);
}

public class OperationResult<T> : OperationResult
{
    public T? Value { get; private init; }

    public static OperationResult<T> Success(T value) => new() { Succeeded = true, Value = value };
    public new static OperationResult<T> Failure(string error, OperationErrorKind kind = OperationErrorKind.Validation) =>
        new() { Succeeded = false, Error = error, ErrorKind = kind };
    public new static OperationResult<T> NotFound(string error) => Failure(error, OperationErrorKind.NotFound);
    public new static OperationResult<T> Conflict(string error) => Failure(error, OperationErrorKind.Conflict);
}
