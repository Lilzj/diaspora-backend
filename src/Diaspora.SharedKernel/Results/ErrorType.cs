namespace Diaspora.SharedKernel.Results;

/// <summary>
/// Categorizes errors for consistent HTTP status code mapping.
/// </summary>
public enum ErrorType
{
    None = 0,
    Failure = 1,
    Validation = 2,
    NotFound = 3,
    Conflict = 4,
    Unauthorized = 5,
    Forbidden = 6,
    Unexpected = 7,
    RateLimited = 8,
    ServiceUnavailable = 9
}
