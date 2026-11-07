using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Services;

/// <summary>
/// Answers the second authorisation question: may this caller do this <em>here</em>?
/// <para>
/// <b>The permission catalogue answers the first — "may this person let property at all" — and it
/// cannot answer this one.</b> Whether the caller owns <em>this</em> listing is a fact about a row,
/// and no token can carry it. Every write handler in the module comes through here to ask.
/// </para>
/// <para>
/// <b>Staff never pass an owner check.</b> A moderator or administrator is not the landlord of a
/// property, and is refused here exactly as any other member is. The owner's decision of 7 Nov
/// 2026: staff never act on a member's content through the member's own routes, and there are no
/// "support edits" made in a landlord's name. Staff act on a property only through the moderation
/// routes (<c>/housing/moderation/*</c>, gated by <see cref="HousingPolicies.Moderate"/>), whose
/// handlers load the listing themselves rather than through <see cref="RequireListingAccessAsync"/>
/// and record the decision on the moderation trail.
/// </para>
/// <para>
/// <b><see cref="IsStaff"/> is for reading, not writing.</b> It lets staff see hidden, draft and
/// deleted properties in <see cref="CanSeeAsync"/> and <see cref="ApplyAudienceAsync"/>, because a
/// moderator has to see what they are reviewing.
/// </para>
/// <para>
/// <b>There is no staff path at all to a member's own viewings or reservations.</b> Reading a named
/// person's appointments — where they are going, and when the property will be empty — is a
/// different act from moderating a public listing. B9 and B10 draw the same line; here it protects
/// a member's physical whereabouts rather than their shopping.
/// </para>
/// </summary>
public sealed class HousingAuthorizationService
{
    private static readonly HashSet<string> StaffRoles =
        new(StringComparer.OrdinalIgnoreCase) { "Moderator", "Admin", "SuperAdmin" };

    private readonly ICurrentUser _currentUser;
    private readonly ILandlordProfileRepository _landlords;

    public HousingAuthorizationService(
        ICurrentUser currentUser, ILandlordProfileRepository landlords)
    {
        _currentUser = currentUser;
        _landlords = landlords;
    }

    /// <summary>The caller, or null when the request is anonymous.</summary>
    public UserId? CallerId => _currentUser.UserId;

    /// <summary>Whether anybody is signed in at all. Read by the address-disclosure rule.</summary>
    public bool IsAuthenticated => _currentUser.UserId is not null;

    /// <summary>
    /// Whether the caller holds a platform staff role. Widens what they may see; it never makes a
    /// property theirs.
    /// </summary>
    public bool IsStaff => _currentUser.Role is { } role && StaffRoles.Contains(role);

    /// <summary>The caller, or a failure. The first line of every write handler.</summary>
    public Result<UserId> RequireCaller() =>
        _currentUser.UserId is { } userId
            ? Result.Success(userId)
            : Result.Failure<UserId>(HousingErrors.NotAuthenticated);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Landlord scope
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The caller's own landlord profile, or a failure telling them to create one.
    /// <para>
    /// A landlord command arrives with a member identity and acts on a landlord identity, and
    /// resolving one to the other in a single place keeps "set up a profile first" a single error
    /// rather than a check each handler remembers differently.
    /// </para>
    /// </summary>
    public async Task<Result<LandlordProfile>> RequireLandlordAsync(
        CancellationToken cancellationToken = default)
    {
        var caller = RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<LandlordProfile>(caller.Error);
        }

        var profile = await _landlords.GetByUserAsync(caller.Value, cancellationToken);

        if (profile is null)
        {
            return Result.Failure<LandlordProfile>(HousingErrors.NotLandlord);
        }

        return profile.Status switch
        {
            LandlordStatus.Suspended =>
                Result.Failure<LandlordProfile>(HousingErrors.LandlordSuspended),
            LandlordStatus.Closed =>
                Result.Failure<LandlordProfile>(HousingErrors.NotLandlord),
            _ => Result.Success(profile)
        };
    }

    /// <summary>
    /// The caller's profile whatever state it is in.
    /// <para>
    /// The read a suspended landlord's own dashboard makes: somebody barred from letting must still
    /// be able to see their properties and read why. Every command uses
    /// <see cref="RequireLandlordAsync"/> instead.
    /// </para>
    /// </summary>
    public async Task<LandlordProfile?> FindCallerLandlordAsync(
        CancellationToken cancellationToken = default) =>
        _currentUser.UserId is { } userId
            ? await _landlords.GetByUserAsync(userId, cancellationToken)
            : null;

    /// <summary>
    /// Confirms the caller may act on a listing, and that its landlord may still let.
    /// <para>
    /// The suspension check is here rather than in the aggregate because it spans two of them: a
    /// suspended landlord's listings stay valid objects, but nobody may work them. Putting the check
    /// at every call site would mean forgetting it at one.
    /// </para>
    /// <para>
    /// Staff are not exempt from either half: a moderator who is not the landlord is refused, and
    /// moderates the property through the moderation routes instead (see the class summary).
    /// </para>
    /// </summary>
    public async Task<Result<LandlordProfile>> RequireListingAccessAsync(
        HousingListing listing, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listing);

        var caller = RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<LandlordProfile>(caller.Error);
        }

        var landlord = await _landlords.GetAsync(listing.LandlordId, cancellationToken);

        if (landlord is null)
        {
            return Result.Failure<LandlordProfile>(HousingErrors.LandlordNotFound);
        }

        if (landlord.OwnerUserId != caller.Value)
        {
            return Result.Failure<LandlordProfile>(HousingErrors.NotListingOwner);
        }

        return landlord.CanLet
            ? Result.Success(landlord)
            : Result.Failure<LandlordProfile>(HousingErrors.LandlordSuspended);
    }

    /// <summary>Whether the caller owns the landlord profile a listing belongs to.</summary>
    public async Task<bool> OwnsAsync(
        HousingListing listing, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listing);

        if (_currentUser.UserId is not { } userId)
        {
            return false;
        }

        var landlord = await _landlords.GetForReadAsync(listing.LandlordId, cancellationToken);

        return landlord?.OwnerUserId == userId;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Visibility
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Whether the caller may see a listing at all.
    /// <para>
    /// The owning landlord and staff see everything of theirs, drafts and hidden listings
    /// included — a landlord locked out of their own moderated listing cannot fix it. Everyone else
    /// sees publicly visible, unhidden listings: public and unlisted alike, because unlisted means
    /// "reachable by link" and the caller evidently has the link.
    /// </para>
    /// </summary>
    public async Task<bool> CanSeeAsync(
        HousingListing listing, CancellationToken cancellationToken = default)
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
                is HousingModerationState.Clear or HousingModerationState.Flagged
            && HousingListingStatusTransitions.IsPubliclyVisible(listing.Status)
            && listing.Visibility is HousingVisibility.Public or HousingVisibility.Unlisted;
    }

    /// <summary>
    /// The audience half of a query filter, filled in from who is asking.
    /// <para>
    /// One method, called by every listing query, so the rule that a landlord sees their own drafts
    /// and nobody else's is written once. A query that forgot to call it gets the public view,
    /// which is the safe direction to fail in.
    /// </para>
    /// </summary>
    public async Task<HousingListingQueryFilter> ApplyAudienceAsync(
        HousingListingQueryFilter filter,
        bool includeOwn = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var landlord = includeOwn ? await FindCallerLandlordAsync(cancellationToken) : null;

        return filter with
        {
            ViewerId = _currentUser.UserId,
            ViewerLandlordId = landlord?.Id,
            ViewerIsStaff = IsStaff,
            IncludeNonPublic = includeOwn && landlord is not null
        };
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Member scope
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
            : Result.Failure<UserId>(HousingErrors.NotSeeker);
    }

    /// <summary>
    /// Confirms the caller is one of the two parties to a viewing or a reservation.
    /// <para>
    /// Answers not-found rather than forbidden when they are not: a 403 on somebody else's viewing
    /// would confirm that a named member has an appointment at a named property.
    /// </para>
    /// </summary>
    public async Task<Result<UserId>> RequirePartyAsync(
        UserId memberUserId,
        Domain.Identifiers.LandlordProfileId landlordId,
        Error notFound,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(memberUserId);
        ArgumentNullException.ThrowIfNull(landlordId);
        ArgumentNullException.ThrowIfNull(notFound);

        var caller = RequireCaller();

        if (caller.IsFailure)
        {
            return caller;
        }

        if (caller.Value == memberUserId)
        {
            return caller;
        }

        var landlord = await _landlords.GetForReadAsync(landlordId, cancellationToken);

        return landlord?.OwnerUserId == caller.Value
            ? caller
            : Result.Failure<UserId>(notFound);
    }
}
