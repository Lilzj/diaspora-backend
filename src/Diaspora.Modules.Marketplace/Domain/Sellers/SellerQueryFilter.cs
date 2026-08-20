using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Policies;

namespace Diaspora.Modules.Marketplace.Domain.Sellers;

/// <summary>What a seller listing can narrow by.</summary>
public sealed record SellerQueryFilter
{
    public string? Text { get; init; }

    public string? CountryCode { get; init; }

    public SellerKind? Kind { get; init; }

    public IReadOnlyCollection<SellerStatus>? Statuses { get; init; }

    /// <summary>Only sellers carrying a live Trust verification.</summary>
    public bool VerifiedOnly { get; init; }

    /// <summary>Only sellers backed by a shop in the Providers directory.</summary>
    public bool StorefrontsOnly { get; init; }

    /// <summary>
    /// Whether suspended and closed profiles are visible. Staff surfaces only — a buyer browsing
    /// storefronts must not be shown one they cannot buy from.
    /// </summary>
    public bool IncludeInactive { get; init; }

    public int Limit { get; init; } = MarketplaceLimits.DefaultPageSize;

    public int EffectiveLimit => Math.Clamp(Limit, 1, MarketplaceLimits.MaxPageSize);
}
