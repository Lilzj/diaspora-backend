using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Viewings;
using Diaspora.Modules.Housing.Extensibility.Privacy;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Housing.Application.Services;

/// <summary>
/// Decides how much of a property's address a viewer sees, and is the only thing that may.
/// <para>
/// <b>This is the module's most consequential read path and it is deliberately a service rather
/// than a mapper helper.</b> A precise address on a public page tells everyone which door is empty
/// on the first of the month, tells a scammer exactly which property to advertise as their own, and
/// tells anybody who dislikes the current occupant where to find them. Getting it right needs three
/// facts a mapper does not have — whether the caller owns the listing, whether they hold a
/// confirmed viewing, and what every registered disclosure policy says — so the decision lives
/// where those can be gathered.
/// </para>
/// <para>
/// <b>Policies may only narrow.</b> The composer takes the most restrictive answer of the listing's
/// own setting and every contributed policy. A contributed class cannot widen disclosure, because
/// the failure mode of one that could is publishing somebody's home address.
/// </para>
/// <para>
/// <b>The result is a redacted response, never a permission flag.</b> There is no path by which a
/// caller obtains the street line without having stated who is asking — which is what makes the
/// rule unforgettable rather than merely documented.
/// </para>
/// </summary>
public sealed class HousingAddressDisclosureService
{
    private readonly IHousingViewingRepository _viewings;
    private readonly IEnumerable<IAddressDisclosurePolicy> _policies;
    private readonly HousingAuthorizationService _authorization;

    public HousingAddressDisclosureService(
        IHousingViewingRepository viewings,
        IEnumerable<IAddressDisclosurePolicy> policies,
        HousingAuthorizationService authorization)
    {
        _viewings = viewings;
        _policies = policies;
        _authorization = authorization;
    }

    /// <summary>
    /// Whether this caller holds a viewing that unlocks the address.
    /// <para>
    /// Exposed separately because the listing response also reports it as a flag, and asking twice
    /// would double the query on the module's hottest read path.
    /// </para>
    /// </summary>
    public async Task<bool> HasUnlockingViewingAsync(
        HousingListing listing, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listing);

        return _authorization.CallerId is { } callerId
            && await _viewings.HasAddressUnlockingViewingAsync(
                listing.Id, callerId, cancellationToken);
    }

    /// <summary>
    /// Produces the address a caller is entitled to.
    /// </summary>
    /// <param name="isOwner">
    /// Supplied by the caller rather than recomputed, because the mapper has already asked and the
    /// answer costs a query.
    /// </param>
    /// <param name="hasUnlockingViewing">
    /// Likewise. Pass the result of <see cref="HasUnlockingViewingAsync"/>.
    /// </param>
    public async Task<PropertyAddressResponse> DiscloseAsync(
        HousingListing listing,
        bool isOwner,
        bool hasUnlockingViewing,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listing);

        var isStaff = _authorization.IsStaff;
        var isAuthenticated = _authorization.IsAuthenticated;

        // Every registered policy is consulted and any one of them can veto. The owner and staff
        // are exempt: a landlord locked out of their own address cannot arrange a viewing, and a
        // moderator investigating a duplicate listing must be able to see that two adverts name
        // one flat.
        var permitted = isOwner || isStaff;

        if (!permitted)
        {
            permitted = true;

            foreach (var policy in _policies)
            {
                if (!await policy.PermitsFullDisclosureAsync(
                        listing,
                        _authorization.CallerId,
                        isOwner,
                        isStaff,
                        cancellationToken))
                {
                    permitted = false;
                    break;
                }
            }
        }

        var disclosed = listing.Address.Disclose(
            isOwner,
            isStaff,
            isAuthenticated,
            hasConfirmedViewing: permitted && hasUnlockingViewing);

        // A policy veto narrows to neighbourhood level regardless of what the listing allowed.
        if (!permitted)
        {
            disclosed = listing.Address
                .WithVisibility(AddressVisibility.NeighborhoodOnly)
                .Disclose(false, false, isAuthenticated, false);
        }

        var isFull = disclosed.Line1 is not null
            && string.Equals(disclosed.Line1, listing.Address.Line1, StringComparison.Ordinal);

        return new PropertyAddressResponse
        {
            Line1 = disclosed.Line1,
            Line2 = disclosed.Line2,
            PostalCode = disclosed.PostalCode,
            Latitude = disclosed.Latitude,
            Longitude = disclosed.Longitude,
            Visibility = listing.Address.Visibility.ToString(),
            IsFullyDisclosed = isFull,
            DisclosureHint = isFull ? null : HintFor(listing.Address.Visibility, permitted)
        };
    }

    /// <summary>
    /// How to get the rest, where there is a way.
    /// <para>
    /// Rendered as a prompt rather than an error, because booking a viewing is the intended path to
    /// an address rather than a workaround for one. A member who is simply told "denied" learns
    /// nothing and books nothing.
    /// </para>
    /// </summary>
    private static string? HintFor(AddressVisibility visibility, bool policyPermitted) =>
        !policyPermitted
            ? null
            : visibility switch
            {
                AddressVisibility.OnViewingConfirmed =>
                    "The full address is shared once a viewing is confirmed.",
                AddressVisibility.AuthenticatedMembers =>
                    "Sign in to see the full address.",
                _ => null
            };
}
