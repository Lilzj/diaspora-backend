using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;

namespace Diaspora.Modules.Marketplace.Application.Dashboards;

/// <summary>
/// The marketplace home surface.
/// <para>
/// <b>Unauthenticated, and it works for a member who has never sold anything.</b> The seller block
/// is null for most people and every discovery rail is still populated — a marketplace whose home
/// screen required a seller profile would be one nobody could browse.
/// </para>
/// </summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record GetMarketplaceDashboardQuery : IQuery<MarketplaceDashboardResponse>
{
    public string? CountryCode { get; init; }

    public string? City { get; init; }

    public Guid? AreaId { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>The radius for the nearby rail. Location's own sentinels are accepted.</summary>
    public double? RadiusKm { get; init; }
}
