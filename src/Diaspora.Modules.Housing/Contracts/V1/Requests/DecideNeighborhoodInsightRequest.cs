namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>
/// A moderator deciding on an insight.
/// <para>
/// A rejection carries a reason the author can read. One without is a rejection the author will
/// simply submit again.
/// </para>
/// </summary>
public sealed record DecideNeighborhoodInsightRequest
{
    /// <summary>Approve, Reject or Retire.</summary>
    public required string Action { get; init; }

    public string? Note { get; init; }
}
