using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Analytics;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Modules.Housing.Extensibility.Views;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>
/// Reads one property.
/// <para>
/// <b>The module's hottest path, and the only query that writes.</b> Recording a view on a read is
/// a deliberate trade: the alternative is an events table nothing needs at any other resolution,
/// and a landlord's most useful signal — how many people looked and how few asked to visit — would
/// then cost an aggregate query to produce.
/// </para>
/// <para>
/// <b>The view never bumps the listing's revision.</b> A member opening a property while its
/// landlord is editing it must not make the landlord's save fail with a conflict.
/// </para>
/// <para>
/// <b>A landlord viewing their own listing is not counted.</b> Otherwise every dashboard refresh
/// inflates the figure the dashboard is displaying, and the conversion rate a landlord acts on
/// becomes a measure of how often they checked it.
/// </para>
/// </summary>
[SideEffecting(Reason = "Records the view that this read caused, without bumping the listing's revision.")]
public sealed class GetHousingListingQueryHandler
    : IQueryHandler<GetHousingListingQuery, HousingListingResponse>
{
    private readonly IHousingListingRepository _listings;
    private readonly IHousingAnalyticsRepository _analytics;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingAuthorizationService _authorization;
    private readonly HousingListingReadContext _read;
    private readonly IHousingViewTracker _views;
    private readonly IClock _clock;

    public GetHousingListingQueryHandler(
        IHousingListingRepository listings,
        IHousingAnalyticsRepository analytics,
        IUnitOfWork unitOfWork,
        HousingAuthorizationService authorization,
        HousingListingReadContext read,
        IHousingViewTracker views,
        IClock clock)
    {
        _listings = listings;
        _analytics = analytics;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _read = read;
        _views = views;
        _clock = clock;
    }

    public async Task<Result<HousingListingResponse>> Handle(
        GetHousingListingQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var listing = await ResolveAsync(request.IdOrSlug, cancellationToken);

        if (listing is null || !await _authorization.CanSeeAsync(listing, cancellationToken))
        {
            return Result.Failure<HousingListingResponse>(HousingErrors.ListingNotFound);
        }

        var response = await _read.ToResponseAsync(listing, cancellationToken);

        if (response is null)
        {
            return Result.Failure<HousingListingResponse>(HousingErrors.ListingNotFound);
        }

        await RecordViewAsync(listing, response.IsOwner, request.RecordView, cancellationToken);

        return Result.Success(response);
    }

    /// <summary>
    /// Counts the view, unless there is a reason not to.
    /// <para>
    /// Loads the listing tracked for the write. The read above used the untracked graph, so this
    /// re-reads rather than mutating an untracked entity EF would ignore.
    /// </para>
    /// </summary>
    private async Task RecordViewAsync(
        HousingListing listing,
        bool isOwner,
        bool requested,
        CancellationToken cancellationToken)
    {
        if (!requested || isOwner || _authorization.IsStaff)
        {
            return;
        }

        var isUnique = _views.RecordView(
            listing.Id, _authorization.CallerId, _clock.UtcNow);

        var tracked = await _listings.GetAsync(listing.Id, cancellationToken);

        if (tracked is null)
        {
            return;
        }

        tracked.RecordView(isUnique);

        var analytics = await _analytics.GetByListingAsync(listing.Id, cancellationToken);

        analytics?.RecordView(_clock.UtcNow, isUnique);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Resolves the route parameter, trying an identifier first and then a slug.
    /// <para>
    /// Identifier first because a GUID is unambiguous and a slug is not — and because a slug that
    /// happens to parse as a GUID does not exist. B4's neighbourhood hub resolves the same way.
    /// </para>
    /// </summary>
    private async Task<HousingListing?> ResolveAsync(
        string idOrSlug, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(idOrSlug, out var id))
        {
            var byId = await _listings.GetForReadAsync(
                HousingListingId.From(id), cancellationToken);

            if (byId is not null)
            {
                return byId;
            }
        }

        var slug = HousingSlug.TryCreate(idOrSlug);

        return slug.IsFailure
            ? null
            : await _listings.GetBySlugAsync(slug.Value, cancellationToken);
    }
}
