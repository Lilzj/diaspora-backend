namespace Diaspora.Modules.Marketplace.Contracts.V1.Requests;

/// <summary>Reports a listing to the marketplace moderation queue.</summary>
public sealed record ReportListingRequest
{
    /// <summary>
    /// <c>Fraud</c>, <c>Duplicate</c>, <c>Spam</c>, <c>Prohibited</c>, <c>Counterfeit</c>,
    /// <c>Miscategorised</c>, <c>Offensive</c> or <c>Other</c>.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// The reporter's own words. Required with <c>Other</c>, because a report nobody can act on
    /// wastes the moderator's time and the reporter's.
    /// </summary>
    public string? Detail { get; init; }
}
