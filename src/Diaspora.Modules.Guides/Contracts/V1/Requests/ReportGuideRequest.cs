namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// Reports the guide. The report goes to Community's reporting workflow — the reason catalogue
/// and the duplicate rule come from there — and on to the guide's Trust moderation case, where
/// staff decide. This module implements none of it.
/// </summary>
public sealed record ReportGuideRequest
{
    /// <summary>A Community report reason name — <c>Misinformation</c>, <c>Spam</c>, <c>Outdated</c>…</summary>
    public required string Reason { get; init; }

    public string? Description { get; init; }
}
