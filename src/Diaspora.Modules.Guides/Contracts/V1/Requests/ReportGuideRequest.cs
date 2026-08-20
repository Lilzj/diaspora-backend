namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// Reports the guide. The report goes to Community's moderation workflow — the reason
/// catalogue, the duplicate rule, the auto-hide threshold and the appeal trail all come from
/// there, and this module implements none of them.
/// </summary>
public sealed record ReportGuideRequest
{
    /// <summary>A Community report reason name — <c>Misinformation</c>, <c>Spam</c>, <c>Outdated</c>…</summary>
    public required string Reason { get; init; }

    public string? Description { get; init; }
}
