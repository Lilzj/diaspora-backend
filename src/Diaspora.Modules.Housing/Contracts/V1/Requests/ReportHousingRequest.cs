namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>
/// Reports a property.
/// <para>
/// A Scam report flags the listing on the first one; every other reason waits for the configured
/// threshold. The reporter identity never leaves the report row.
/// </para>
/// </summary>
public sealed record ReportHousingRequest
{
    /// <summary>
    /// Scam, Duplicate, Misleading, AlreadyTaken, WrongLocation, Discriminatory, UnsafeProperty,
    /// Offensive, NotTheOwner or Other.
    /// </summary>
    public required string Reason { get; init; }

    public string? Detail { get; init; }
}
