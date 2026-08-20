namespace Diaspora.Modules.Providers.Integration;

/// <summary>
/// One provider, described in terms every consuming module can read.
/// <para>
/// <b>Flat primitives on purpose.</b> It crosses a module boundary, and a consumer that had to
/// reference B7's value objects to read a country code would be coupled to this module's
/// internals rather than to its contract — the same reasoning B4 records over
/// <c>ResidencySummary</c>. A Marketplace listing that wants to show "sold by Mama Afrika
/// Kitchen ✓" needs a name, a slug, a verified flag and a cover reference, and nothing else.
/// </para>
/// <para>
/// <b>Media is a <c>Guid</c>, not a URL</b>, per ADR-005. The consumer resolves it through
/// Media's link endpoint exactly as it resolves its own.
/// </para>
/// </summary>
public sealed record ProviderSummary
{
    public required Guid ProviderId { get; init; }

    /// <summary>The deep-link handle. Stable across renames.</summary>
    public required string Slug { get; init; }

    public required string Name { get; init; }

    /// <summary>The <c>ProviderType</c> name — <c>Business</c>, <c>Church</c>.</summary>
    public required string ProviderType { get; init; }

    /// <summary>The <c>ProviderStatus</c> name. A consumer should render only <c>Active</c>.</summary>
    public required string Status { get; init; }

    public required Guid CategoryId { get; init; }

    public required string CategoryName { get; init; }

    public string? CategorySlug { get; init; }

    /// <summary>The rolled-up answer. What a badge renders from.</summary>
    public required bool IsVerified { get; init; }

    public required bool IsFeatured { get; init; }

    public required double AverageRating { get; init; }

    public required int TotalReviews { get; init; }

    public string? CountryCode { get; init; }

    public Guid? AreaId { get; init; }

    public string? AreaName { get; init; }

    public string? CityName { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>A Media identifier. Never a URL.</summary>
    public Guid? CoverMediaId { get; init; }

    /// <summary>A Media identifier. Never a URL.</summary>
    public Guid? LogoMediaId { get; init; }

    /// <summary>The member who owns it, for a consumer that needs to attribute or message.</summary>
    public required Guid OwnerId { get; init; }
}
