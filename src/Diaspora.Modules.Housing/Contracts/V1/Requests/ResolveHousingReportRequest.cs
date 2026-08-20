namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>
/// Closes a report.
/// <para>
/// Upholding and dismissing are recorded distinctly: a landlord with three upheld reports is a
/// different proposition from one with three dismissed ones, and a queue that recorded only
/// "closed" would lose exactly that distinction.
/// </para>
/// </summary>
public sealed record ResolveHousingReportRequest
{
    public required bool Upheld { get; init; }

    public string? Resolution { get; init; }

    /// <summary>Whether to hide the listing as part of upholding.</summary>
    public bool HideListing { get; init; }
}
