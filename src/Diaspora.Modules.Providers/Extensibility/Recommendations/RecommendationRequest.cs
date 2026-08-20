using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Providers.Extensibility.Recommendations;

/// <summary>
/// What a recommender is asked.
/// <para>
/// Covers both shapes the product needs from one interface: "more like this one"
/// (<see cref="SeedProviderId"/> set — the client's Similar Providers strip) and "things for
/// this member" (<see cref="ViewerId"/> set, no seed — a future home-screen rail). A recommender
/// that only handles one returns an empty list for the other and the next one runs.
/// </para>
/// </summary>
public sealed record RecommendationRequest
{
    /// <summary>The provider being viewed, for "more like this".</summary>
    public ProviderId? SeedProviderId { get; init; }

    /// <summary>The member, for personalised suggestions.</summary>
    public UserId? ViewerId { get; init; }

    /// <summary>Narrow to a category, when the caller has one in mind.</summary>
    public ServiceCategoryId? CategoryId { get; init; }

    public string? CountryCode { get; init; }

    public Guid? AreaId { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>The most suggestions to return.</summary>
    public required int Limit { get; init; }

    /// <summary>Providers already on screen. A recommender must not suggest what is visible.</summary>
    public IReadOnlyCollection<ProviderId> Exclude { get; init; } = [];
}
