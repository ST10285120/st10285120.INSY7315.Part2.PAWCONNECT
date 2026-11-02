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
