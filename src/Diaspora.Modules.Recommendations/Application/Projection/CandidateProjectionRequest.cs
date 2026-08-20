using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Application.Projection;

/// <summary>
/// What a consumer wants projected into the candidate table.
/// <para>
/// <b>Every property is optional and null means "unchanged", which is the only sound reading for a
/// projection assembled from several contracts.</b> Contracts are not all the same shape: a
/// <c>ListingUpdated</c> event carries a title and no location, and treating its silence about
/// location as "the listing has no location" would blank the field every time somebody fixed a typo.
/// </para>
/// <para>
/// <b>The three required fields are the identity and the source.</b> Everything else is enrichment
/// that some contract will carry and others will not — which is exactly why a consumer supplies what
/// its own event happens to know and nothing more.
/// </para>
/// </summary>
public sealed record CandidateProjectionRequest
{
    public required RecommendationTargetType TargetType { get; init; }

    public required Guid TargetId { get; init; }

    /// <summary>Which module published the contract this was projected from.</summary>
    public required string SourceModule { get; init; }

    public string? Title { get; init; }

    public string? Summary { get; init; }

    public string? Slug { get; init; }

    /// <summary>An opaque Media reference. Never a URL — B5's rule.</summary>
    public string? MediaReference { get; init; }

    public string? CategoryKey { get; init; }

    public string? CountryCode { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    public string? NeighborhoodName { get; init; }

    public string? LanguageCode { get; init; }

    public UserId? OwnerUserId { get; init; }

    public bool? IsVerified { get; init; }

    public bool? IsFeatured { get; init; }

    /// <summary>0–1, normalised by the consumer against its own vertical.</summary>
    public decimal? PopularityScore { get; init; }

    /// <summary>0–1. How much the platform can vouch for it.</summary>
    public decimal? QualityScore { get; init; }

    public long? EngagementCount { get; init; }

    public CandidateVisibility? Visibility { get; init; }

    public int? MinimumTrustLevel { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public DateTimeOffset? SourceUpdatedAt { get; init; }

    /// <summary>When it stops being recommendable — a closing date, an event's start.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Tags, skills and amenities, in the shared interest vocabulary.</summary>
    public IReadOnlyList<(InterestKind Kind, string Key)> Attributes { get; init; } = [];

    /// <summary>
    /// Replaces the attribute set rather than merging into it.
    /// <para>
    /// False by default, because most events carry no attributes at all and a merge that treated
    /// silence as "remove everything" would strip a job of its skills every time its title changed.
    /// </para>
    /// </summary>
    public bool ReplaceAttributes { get; init; }
}
