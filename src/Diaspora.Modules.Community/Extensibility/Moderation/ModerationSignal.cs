using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.ValueObjects;

namespace Diaspora.Modules.Community.Extensibility.Moderation;

/// <summary>Everything known about a piece of content at the moment a policy is consulted.</summary>
public sealed record ModerationSignal
{
    public required ContentReference Content { get; init; }

    public required Guid AuthorId { get; init; }

    public int OpenReportCount { get; init; }

    /// <summary>The most-cited reason among the open reports.</summary>
    public ReportReasonCode? DominantReason { get; init; }

    public ContentReviewStatus ReviewStatus { get; init; } = ContentReviewStatus.Pending;

    public IReadOnlyCollection<ContentReviewFlag> ReviewFlags { get; init; } = [];

    public double ReviewConfidence { get; init; }

    /// <summary>The author's platform trust level, as an ordinal. Never re-derived here.</summary>
    public int AuthorTrustLevel { get; init; }

    public string? CountryCode { get; init; }
}
