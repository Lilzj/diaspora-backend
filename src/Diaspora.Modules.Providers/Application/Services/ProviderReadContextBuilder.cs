using Diaspora.Modules.Location.Integration;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Domain.Reviews;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Providers.Application.Services;

/// <summary>
/// Assembles everything a page of providers needs that is not on the providers themselves.
/// <para>
/// <b>One place, called once per request.</b> Categories, distances, availability, the viewer's
/// roles, their bookmarks and their own reviews are each a batched lookup, and the alternative —
/// resolving them inside the mapper, per row — is six N+1s stacked on one another. That is the
/// difference between a discovery page that is one query plus six and one that is a hundred and
/// twenty.
/// </para>
/// <para>
/// Every dependency here is a published contract of another module or of B7's own repositories.
/// Nothing reaches into Identity, Trust, Location, Media or Community past the seams their
/// contract freezes name.
/// </para>
/// </summary>
public sealed class ProviderReadContextBuilder
{
    private readonly ServiceCategoryResolver _categories;
    private readonly AvailabilityCalculator _availability;
    private readonly ProviderBookmarkService _bookmarks;
    private readonly IProviderRepository _providers;
    private readonly IProviderReviewRepository _reviews;
    private readonly ProviderAuthorizationService _authorization;
    private readonly IClock _clock;

    public ProviderReadContextBuilder(
        ServiceCategoryResolver categories,
        AvailabilityCalculator availability,
        ProviderBookmarkService bookmarks,
        IProviderRepository providers,
        IProviderReviewRepository reviews,
        ProviderAuthorizationService authorization,
        IClock clock)
    {
        _categories = categories;
        _availability = availability;
        _bookmarks = bookmarks;
        _providers = providers;
        _reviews = reviews;
        _authorization = authorization;
        _clock = clock;
    }

    /// <summary>
    /// Builds the context for a page of providers.
    /// <para>
    /// <paramref name="includeViewerReviews"/> is off for a list and on for a detail read. A
    /// discovery page does not render whether you reviewed each card, and asking would be twenty
    /// rows of a query nothing displays; a profile screen decides whether to show a review form
    /// from exactly that answer.
    /// </para>
    /// </summary>
    public async Task<ProviderReadContext> BuildAsync(
        IReadOnlyList<Provider> providers,
        GeographicScope? scope = null,
        IReadOnlyDictionary<Guid, double>? distances = null,
        string? languageCode = null,
        bool includeViewerReviews = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var now = _clock.UtcNow;
        var viewerId = _authorization.CallerId;
        var ids = providers.Select(provider => provider.Id).ToList();

        var categories = await _categories.LookupAsync(
            providers.Select(provider => provider.CategoryId).ToList(), cancellationToken);

        var availability = await _availability.EvaluateManyAsync(
            providers, viewerId, now, cancellationToken);

        var roles = viewerId is null
            ? new Dictionary<Guid, Domain.Enums.ProviderRole>()
            : await _providers.GetRolesForAsync(viewerId, ids, cancellationToken);

        var bookmarked = await _bookmarks.FilterSavedAsync(viewerId, ids, cancellationToken);

        var viewerReviews = includeViewerReviews && viewerId is not null
            ? await ViewerReviewsAsync(viewerId, ids, cancellationToken)
            : new Dictionary<Guid, Guid>();

        return new ProviderReadContext
        {
            At = now,
            Today = DateOnly.FromDateTime(now.UtcDateTime),
            ViewerId = viewerId,
            LanguageCode = languageCode,
            Categories = categories,
            DistancesKm = distances ?? new Dictionary<Guid, double>(),
            Availability = availability,
            ViewerRoles = roles,
            BookmarkedProviderIds = bookmarked,
            ViewerReviewIds = viewerReviews,
            IsStaff = _authorization.IsStaff
        };
    }

    /// <summary>
    /// Which of these providers the viewer has already reviewed.
    /// <para>
    /// A loop rather than one batched query, and it is bounded to a single provider at every
    /// real call site — this is only requested on a detail read. A batched form would be a
    /// better shape the day a screen needs it, and adding one before then would be a repository
    /// method with no caller and no test.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, Guid>> ViewerReviewsAsync(
        UserId viewerId, IReadOnlyList<ProviderId> providerIds, CancellationToken cancellationToken)
    {
        var found = new Dictionary<Guid, Guid>();

        foreach (var providerId in providerIds)
        {
            var review = await _reviews.FindByReviewerAsync(providerId, viewerId, cancellationToken);

            if (review is not null)
            {
                found[providerId.Value] = review.Id.Value;
            }
        }

        return found;
    }
}
