using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Services;

/// <summary>
/// Answers the second authorisation question: may this caller do this <em>here</em>?
/// <para>
/// <b>The permission catalogue answers the first — "may this person sell at all" — and it cannot
/// answer this one.</b> Whether the caller owns <em>this</em> listing is a fact about a row, and no
/// token can carry it. Every write handler in the module comes through here to ask.
/// </para>
/// <para>
/// <b>One implementation, used by every write handler.</b> Two dozen commands mutate a listing, an
/// offer or a reservation, and each needs the same check. Written two dozen times it would be
/// written most times correctly, and the exception would be a member editing somebody else's
/// listing.
/// </para>
/// <para>
/// <b>Staff never pass an owner check.</b> A moderator or administrator is not the owner of a
/// listing, a seller profile, an offer or a reservation, and is refused here exactly as any other
/// member is. The owner's decision of 7 Nov 2026: staff never act on a member's content through the
/// member's own routes, and there are no "support edits" made in a seller's name. Staff act on a
/// listing or a seller only through the moderation routes (<c>/marketplace/moderation/*</c>, gated
/// by <see cref="MarketplacePolicies.Moderate"/>), whose handlers do not come through
/// <see cref="RequireListingAccessAsync"/> and which record the decision as moderation.
/// </para>
/// <para>
/// <b><see cref="IsStaff"/> is for reading, not writing.</b> It lets staff see hidden, draft and
/// deleted listings in <see cref="CanSeeAsync"/> and <see cref="ApplyAudienceAsync"/>, because a
/// moderator has to see what they are reviewing. A buyer's own things — a named person's
/// watchlist, offers and reservations — have no staff path at all, not even to read.
/// </para>
/// </summary>
public sealed class MarketplaceAuthorizationService
{
    private static readonly HashSet<string> StaffRoles =
        new(StringComparer.OrdinalIgnoreCase) { "Moderator", "Admin", "SuperAdmin" };

    private readonly ICurrentUser _currentUser;
    private readonly ISellerProfileRepository _sellers;

    public MarketplaceAuthorizationService(
        ICurrentUser currentUser, ISellerProfileRepository sellers)
    {
        _currentUser = currentUser;
        _sellers = sellers;
    }

    /// <summary>The caller, or null when the request is anonymous.</summary>
    public UserId? CallerId => _currentUser.UserId;

    /// <summary>
    /// Whether the caller holds a platform staff role. Widens what they may see and gates the
    /// moderation handlers; it never makes a listing theirs.
    /// </summary>
    public bool IsStaff => _currentUser.Role is { } role && StaffRoles.Contains(role);

    /// <summary>The caller, or a failure. The first line of every write handler.</summary>
    public Result<UserId> RequireCaller() =>
        _currentUser.UserId is { } userId
            ? Result.Success(userId)
            : Result.Failure<UserId>(MarketplaceErrors.NotAuthenticated);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Seller scope
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The caller's own seller profile, or a failure telling them to create one.
    /// <para>
    /// A seller command arrives with a member identity and acts on a seller identity, and resolving
    /// one to the other in a single place keeps "set up a profile first" a single error rather than
    /// a check each handler remembers differently.
    /// </para>
    /// </summary>
    public async Task<Result<SellerProfile>> RequireSellerAsync(
        CancellationToken cancellationToken = default)
    {
        var caller = RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<SellerProfile>(caller.Error);
        }

        var profile = await _sellers.GetByUserAsync(caller.Value, cancellationToken);

        if (profile is null)
        {
            return Result.Failure<SellerProfile>(MarketplaceErrors.NotSeller);
        }

        return profile.Status switch
        {
            SellerStatus.Suspended => Result.Failure<SellerProfile>(
                MarketplaceErrors.SellerSuspended),
            SellerStatus.Closed => Result.Failure<SellerProfile>(MarketplaceErrors.NotSeller),
            _ => Result.Success(profile)
        };
    }

    /// <summary>
    /// The caller's profile whatever state it is in.
    /// <para>
    /// The read a suspended seller's own dashboard makes: somebody who has been barred from trading
    /// must still be able to see their listings and read why. Every command uses
    /// <see cref="RequireSellerAsync"/> instead.
    /// </para>
    /// </summary>
    public async Task<SellerProfile?> FindCallerSellerAsync(
        CancellationToken cancellationToken = default) =>
        _currentUser.UserId is { } userId
            ? await _sellers.GetByUserAsync(userId, cancellationToken)
            : null;

    /// <summary>
    /// Confirms the caller may act on a listing, and that its seller may still trade.
    /// <para>
    /// The suspension check is here rather than in the aggregate because it spans two of them: a
    /// suspended seller's listings stay valid objects, but nobody may work them. Putting the check
    /// at every call site would mean forgetting it at one.
    /// </para>
    /// <para>
    /// Staff are not exempt from either half: a moderator who does not own the listing is refused,
    /// and moderates it through the moderation routes instead (see the class summary).
    /// </para>
    /// </summary>
    public async Task<Result<SellerProfile>> RequireListingAccessAsync(
        MarketplaceListing listing, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listing);

        var caller = RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<SellerProfile>(caller.Error);
        }

        var seller = await _sellers.GetAsync(listing.SellerId, cancellationToken);

        if (seller is null)
        {
            return Result.Failure<SellerProfile>(MarketplaceErrors.SellerNotFound);
        }

        if (seller.OwnerUserId != caller.Value)
        {
            return Result.Failure<SellerProfile>(MarketplaceErrors.NotListingOwner);
        }

        return seller.CanTrade
            ? Result.Success(seller)
            : Result.Failure<SellerProfile>(MarketplaceErrors.SellerSuspended);
    }

    /// <summary>Whether the caller owns the seller profile a listing belongs to.</summary>
    public async Task<bool> OwnsAsync(
        MarketplaceListing listing, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listing);

        if (_currentUser.UserId is not { } userId)
        {
            return false;
        }

        var seller = await _sellers.GetForReadAsync(listing.SellerId, cancellationToken);

        return seller?.OwnerUserId == userId;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Visibility
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Whether the caller may see a listing at all.
    /// <para>
    /// The owning seller and staff see everything of theirs, drafts and hidden listings included —
    /// a seller locked out of their own moderated listing cannot fix it. Everyone else sees
    /// publicly visible, unhidden listings: public and unlisted alike, because unlisted means
    /// "reachable by link" and the caller evidently has the link.
    /// </para>
    /// </summary>
    public async Task<bool> CanSeeAsync(
        MarketplaceListing listing, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listing);

        if (IsStaff)
        {
            return true;
        }

        if (listing.IsDeleted)
        {
            return false;
        }

        if (await OwnsAsync(listing, cancellationToken))
        {
            return true;
        }

        return listing.ModerationState
                is ListingModerationState.Clear or ListingModerationState.Flagged
            && ListingStatusTransitions.IsPubliclyVisible(listing.Status)
            && listing.Visibility is ListingVisibility.Public or ListingVisibility.Unlisted;
    }

    /// <summary>
    /// The audience half of a query filter, filled in from who is asking.
    /// <para>
    /// One method, called by every listing query, so the rule that a member sees their own drafts
    /// and nobody else's is written once. A query that forgot to call it gets the public view,
    /// which is the safe direction to fail in.
    /// </para>
    /// </summary>
    public async Task<ListingQueryFilter> ApplyAudienceAsync(
        ListingQueryFilter filter,
        bool includeOwn = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var seller = includeOwn ? await FindCallerSellerAsync(cancellationToken) : null;

        return filter with
        {
            ViewerId = _currentUser.UserId,
            ViewerSellerId = seller?.Id,
            ViewerIsStaff = IsStaff,
            IncludeNonPublic = includeOwn && seller is not null
        };
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Buyer scope
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Confirms the caller is the member something belongs to.
    /// <para>
    /// Staff are deliberately <b>not</b> given a bypass here — see the class summary.
    /// </para>
    /// </summary>
    public Result<UserId> RequireOwner(UserId ownerId)
    {
        ArgumentNullException.ThrowIfNull(ownerId);

        var caller = RequireCaller();

        if (caller.IsFailure)
        {
            return caller;
        }

        return caller.Value == ownerId
            ? caller
            : Result.Failure<UserId>(MarketplaceErrors.NotBuyer);
    }
}
