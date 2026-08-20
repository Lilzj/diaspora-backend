using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Viewings;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Viewings;

/// <summary>
/// The caller's viewings, as a member or as a landlord.
/// <para>
/// <b>Always scoped to the caller, and staff get no bypass.</b> A list of who is visiting which
/// empty property and when is not something a role check in a general-purpose helper should hand
/// out; if support ever needs it, that should be a deliberate tool with its own audit trail.
/// </para>
/// </summary>
public sealed class ListViewingsQueryHandler
    : IQueryHandler<ListViewingsQuery, IReadOnlyList<HousingViewingResponse>>
{
    private readonly IHousingViewingRepository _viewings;
    private readonly IHousingListingRepository _listings;
    private readonly ILandlordProfileRepository _landlords;
    private readonly HousingAuthorizationService _authorization;

    public ListViewingsQueryHandler(
        IHousingViewingRepository viewings,
        IHousingListingRepository listings,
        ILandlordProfileRepository landlords,
        HousingAuthorizationService authorization)
    {
        _viewings = viewings;
        _listings = listings;
        _landlords = landlords;
        _authorization = authorization;
    }

    public async Task<Result<IReadOnlyList<HousingViewingResponse>>> Handle(
        ListViewingsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<HousingViewingResponse>>(caller.Error);
        }

        var asLandlord = string.Equals(
            request.Side, "landlord", StringComparison.OrdinalIgnoreCase);

        var landlord = asLandlord
            ? await _authorization.FindCallerLandlordAsync(cancellationToken)
            : null;

        // Asking for the landlord side without a landlord profile is an empty diary rather than an
        // error: a member who has not set one up simply has no properties to show.
        if (asLandlord && landlord is null)
        {
            return Result.Success<IReadOnlyList<HousingViewingResponse>>([]);
        }

        var filter = new ViewingQueryFilter
        {
            SeekerUserId = asLandlord ? null : caller.Value,
            LandlordId = landlord?.Id,
            ListingId = request.ListingId is { } listingId && listingId != Guid.Empty
                ? HousingListingId.From(listingId)
                : null,
            OpenOnly = request.OpenOnly,
            Limit = request.Limit ?? Domain.Policies.HousingLimits.DefaultPageSize
        };

        var page = await _viewings.QueryAsync(
            filter, request.Cursor, filter.EffectiveLimit, cancellationToken);

        return Result.Success(await ProjectAsync(page.Items, cancellationToken));
    }

    /// <summary>
    /// Resolves the listing titles and landlord names a page needs, batched.
    /// <para>
    /// A diary of twenty viewings that resolved its listings one at a time would be forty round
    /// trips to render twenty rows.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<HousingViewingResponse>> ProjectAsync(
        IReadOnlyList<HousingViewing> viewings, CancellationToken cancellationToken)
    {
        if (viewings.Count == 0)
        {
            return [];
        }

        var listings = (await _listings.GetManyAsync(
                [.. viewings.Select(viewing => viewing.ListingId).Distinct()],
                cancellationToken))
            .ToDictionary(listing => listing.Id);

        var landlords = await _landlords.GetManyAsync(
            [.. viewings.Select(viewing => viewing.LandlordId).Distinct()], cancellationToken);

        return
        [
            .. viewings.Select(viewing => HousingRelocationMapper.ToResponse(
                viewing,
                listings.GetValueOrDefault(viewing.ListingId),
                landlords.GetValueOrDefault(viewing.LandlordId)))
        ];
    }
}
