namespace Diaspora.Modules.Marketplace.Contracts.V1.Requests;

/// <summary>Closes a report, upheld or not.</summary>
public sealed record ResolveReportRequest
{
    /// <summary><c>Actioned</c> or <c>Dismissed</c>.</summary>
    public required string Outcome { get; init; }

    /// <summary>What the moderator decided. Kept for the audit trail, never sent verbatim.</summary>
    public string? Resolution { get; init; }
}
