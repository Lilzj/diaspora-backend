using Diaspora.Modules.Providers.Contracts.V1.Responses;
using Diaspora.Modules.Providers.Domain.Categories;
using Diaspora.Modules.Providers.Extensibility.Availability;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Providers.Application.Services;

/// <summary>
/// Everything a page of providers needs that is not on the providers themselves.
/// <para>
/// <b>Assembled once per request, not per row.</b> Category names, distances, availability
/// answers, the viewer's roles and their bookmark set are all cross-cutting facts, and resolving
/// each one inside the mapper would turn a twenty-card page into twenty lookups apiece —
/// exactly the N+1 that makes a directory feel slow in production and fine in development.
/// </para>
/// <para>
/// Every dictionary is keyed by the raw <see cref="Guid"/> rather than a strongly typed id.
/// The mapper is hot and does one lookup per field per row; a record key would allocate on every
/// one, and the type safety buys nothing inside a dictionary that never leaves this file's
/// neighbourhood.
/// </para>
/// </summary>
public sealed record ProviderReadContext
{
    /// <summary>Now, so every row on a page answers "open?" against the same instant.</summary>
    public required DateTimeOffset At { get; init; }

    public required DateOnly Today { get; init; }

    public UserId? ViewerId { get; init; }

    /// <summary>The caller's preferred language, for category translation.</summary>
    public string? LanguageCode { get; init; }

    /// <summary>Resolved categories, keyed by identifier.</summary>
    public IReadOnlyDictionary<Guid, ServiceCategory> Categories { get; init; } =
        new Dictionary<Guid, ServiceCategory>();

    /// <summary>Distances from Location, keyed by provider. Empty when the scope had no point.</summary>
    public IReadOnlyDictionary<Guid, double> DistancesKm { get; init; } =
        new Dictionary<Guid, double>();

    /// <summary>Availability answers, keyed by provider.</summary>
    public IReadOnlyDictionary<Guid, AvailabilityAnswer> Availability { get; init; } =
        new Dictionary<Guid, AvailabilityAnswer>();

    /// <summary>The viewer's role on each provider they hold one on.</summary>
    public IReadOnlyDictionary<Guid, Domain.Enums.ProviderRole> ViewerRoles { get; init; } =
        new Dictionary<Guid, Domain.Enums.ProviderRole>();

    /// <summary>Providers the viewer has bookmarked. Sourced from Community.</summary>
    public IReadOnlySet<Guid> BookmarkedProviderIds { get; init; } = new HashSet<Guid>();

    /// <summary>Providers the viewer follows. Sourced from Community.</summary>
    public IReadOnlySet<Guid> FollowedProviderIds { get; init; } = new HashSet<Guid>();

    /// <summary>The viewer's own review of each provider they have reviewed.</summary>
    public IReadOnlyDictionary<Guid, Guid> ViewerReviewIds { get; init; } =
        new Dictionary<Guid, Guid>();

    /// <summary>Providers the viewer has already recommended.</summary>
    public IReadOnlySet<Guid> RecommendedProviderIds { get; init; } = new HashSet<Guid>();

    /// <summary>Display names for owners and reviewers, keyed by user identifier.</summary>
    public IReadOnlyDictionary<Guid, string> DisplayNames { get; init; } =
        new Dictionary<Guid, string>();

    /// <summary>Whether the caller holds a staff role, for statistics and hidden reviews.</summary>
    public bool IsStaff { get; init; }

    public ProviderCategoryResponse CategoryFor(Guid categoryId) =>
        Categories.TryGetValue(categoryId, out var category)
            ? ServiceCategoryMappingHelper.ToSummary(category, LanguageCode)
            : ServiceCategoryMappingHelper.Unknown(categoryId);

    public AvailabilityAnswer AvailabilityFor(Guid providerId) =>
        Availability.TryGetValue(providerId, out var answer) ? answer : AvailabilityAnswer.Unknown;

    public double? DistanceFor(Guid providerId) =>
        DistancesKm.TryGetValue(providerId, out var distance) ? distance : null;

    public string? DisplayNameFor(Guid userId) =>
        DisplayNames.TryGetValue(userId, out var name) ? name : null;
}
