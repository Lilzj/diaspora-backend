using Diaspora.Application.Abstractions;
using Diaspora.Modules.Trust.Application.Mapping;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Contracts.V1.Responses;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Extensibility;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Trust.Application.Profile;

/// <summary>
/// The caller's trust profile — everything the Trust &amp; Safety Center renders.
/// <para>
/// Deliberately one call. The screen currently assembles this from four sources on the device
/// (auth store, trust store, a client-side computation and a seeded fixture), and a trust
/// centre whose parts can disagree about whether you are verified is worse than no trust
/// centre.
/// </para>
/// </summary>
[RequirePermission(TrustPermissions.ReadSelf)]
public sealed record GetMyTrustProfileQuery : IQuery<TrustProfileResponse>;

public sealed class GetMyTrustProfileQueryHandler
    : IQueryHandler<GetMyTrustProfileQuery, TrustProfileResponse>
{
    private readonly ITrustProfileRepository _profiles;
    private readonly IVerificationRequestRepository _verifications;
    private readonly VerificationTypeHandlerRegistry _handlers;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public GetMyTrustProfileQueryHandler(
        ITrustProfileRepository profiles,
        IVerificationRequestRepository verifications,
        VerificationTypeHandlerRegistry handlers,
        ICurrentUser currentUser,
        IClock clock)
    {
        _profiles = profiles;
        _verifications = verifications;
        _handlers = handlers;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<TrustProfileResponse>> Handle(
        GetMyTrustProfileQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (userId is null)
        {
            return Result.Failure<TrustProfileResponse>(TrustErrors.NotAuthenticated);
        }

        var now = _clock.UtcNow;

        var profile = await _profiles.GetByUserReadOnlyAsync(userId, cancellationToken);

        // A member who has never touched a trust feature has no profile yet. Returning a
        // 404 here would make the trust centre unopenable for exactly the people it most
        // needs to guide, so an empty profile is synthesised for the read.
        profile ??= TrustProfile.Open(userId, now);

        var verifications = await BuildVerificationMapAsync(userId, cancellationToken);

        // Risk is staff-facing. The member's own view omits it entirely — the frontend shows
        // it behind an "Admin view" label, but a number the client receives is a number the
        // member can read.
        var includeRisk = _currentUser.Permissions.Contains(TrustPermissions.ReviewVerification);

        return Result.Success(profile.ToResponse(verifications, now, includeRisk));
    }

    /// <summary>
    /// Status per verification type, for the checklist.
    /// <para>
    /// Every supported type appears, whether or not the member has attempted it — the client
    /// indexes this map by type name and renders a row per entry, so a missing key is a
    /// missing row rather than an empty one.
    /// </para>
    /// <para>
    /// <b>Types never attempted read <see cref="NotStarted"/>, not <c>Pending</c>.</b> Until
    /// 20 Oct 2026 they read <c>Pending</c>, which is also what a submitted case awaiting review
    /// reads, so the trust centre could not tell "you have not done this" from "we are looking at
    /// it" and showed the second to every member for every type. <c>Pending</c> now means a real
    /// open case. <c>NotStarted</c> is a value of this map only, never a stored status.
    /// </para>
    /// </summary>
    internal const string NotStarted = "NotStarted";

    private async Task<IReadOnlyDictionary<string, string>> BuildVerificationMapAsync(
        UserId userId, CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var type in _handlers.SupportedTypes)
        {
            map[type.ToString()] = NotStarted;
        }

        var page = await _verifications.ListForUserAsync(
            userId,
            new PaginationRequest { Page = 1, PageSize = PaginationRequest.MaxPageSize },
            cancellationToken: cancellationToken);

        // Newest wins: a member who was rejected and later approved shows Approved.
        foreach (var verification in page.Items.OrderBy(item => item.SubmittedAt))
        {
            map[verification.Type.ToString()] = verification.Status.ToString();
        }

        return map;
    }
}

/// <summary>
/// Another member's public trust standing.
/// <para>
/// Feeds the contributor directory, guide bylines and the trust badges rendered beside
/// listings. Carries no risk score, no health reasons and no violation counts — see
/// <see cref="PublicTrustProfileResponse"/> on why that is a separate type rather than
/// nulled fields.
/// </para>
/// </summary>
[RequirePermission(TrustPermissions.ReadPublic)]
public sealed record GetPublicTrustProfileQuery : IQuery<PublicTrustProfileResponse>
{
    public required Guid UserId { get; init; }
}

public sealed class GetPublicTrustProfileQueryHandler
    : IQueryHandler<GetPublicTrustProfileQuery, PublicTrustProfileResponse>
{
    private readonly ITrustProfileRepository _profiles;
    private readonly IReputationProfileRepository _reputations;

    public GetPublicTrustProfileQueryHandler(
        ITrustProfileRepository profiles, IReputationProfileRepository reputations)
    {
        _profiles = profiles;
        _reputations = reputations;
    }

    public async Task<Result<PublicTrustProfileResponse>> Handle(
        GetPublicTrustProfileQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = UserId.From(request.UserId);

        var profile = await _profiles.GetByUserReadOnlyAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<PublicTrustProfileResponse>(TrustErrors.ProfileNotFound);
        }

        var reputation = await _reputations.GetByUserReadOnlyAsync(userId, cancellationToken);

        return Result.Success(
            profile.ToPublicResponse(reputation?.Level ?? ReputationLevel.Member));
    }
}

/// <summary>The caller's trust timeline — the client's "Trust Journey" tab.</summary>
[RequirePermission(TrustPermissions.ReadSelf)]
public sealed record GetTrustTimelineQuery : IQuery<PagedResult<TrustTimelineEventResponse>>
{
    public PaginationRequest Pagination { get; init; } = new();
}

public sealed class GetTrustTimelineQueryHandler
    : IQueryHandler<GetTrustTimelineQuery, PagedResult<TrustTimelineEventResponse>>
{
    private readonly ITrustEventRepository _timeline;
    private readonly ICurrentUser _currentUser;

    public GetTrustTimelineQueryHandler(ITrustEventRepository timeline, ICurrentUser currentUser)
    {
        _timeline = timeline;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<TrustTimelineEventResponse>>> Handle(
        GetTrustTimelineQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;
        if (userId is null)
        {
            return Result.Failure<PagedResult<TrustTimelineEventResponse>>(TrustErrors.NotAuthenticated);
        }

        // Staff investigating a case see the internal entries too; a member never does.
        var memberVisibleOnly = !_currentUser.Permissions.Contains(TrustPermissions.ReviewVerification);

        var page = await _timeline.ListForUserAsync(
            userId, request.Pagination.Sanitize(), memberVisibleOnly, cancellationToken);

        return Result.Success(page.Map(item => item.ToResponse()));
    }
}

/// <summary>The caller's active badges.</summary>
[RequirePermission(TrustPermissions.ReadSelf)]
public sealed record GetMyBadgesQuery : IQuery<IReadOnlyList<BadgeResponse>>;

public sealed class GetMyBadgesQueryHandler
    : IQueryHandler<GetMyBadgesQuery, IReadOnlyList<BadgeResponse>>
{
    private readonly ITrustProfileRepository _profiles;
    private readonly ICurrentUser _currentUser;

    public GetMyBadgesQueryHandler(ITrustProfileRepository profiles, ICurrentUser currentUser)
    {
        _profiles = profiles;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<BadgeResponse>>> Handle(
        GetMyBadgesQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (userId is null)
        {
            return Result.Failure<IReadOnlyList<BadgeResponse>>(TrustErrors.NotAuthenticated);
        }

        var profile = await _profiles.GetByUserReadOnlyAsync(userId, cancellationToken);

        IReadOnlyList<BadgeResponse> badges = profile is null
            ? []
            : profile.Badges
                .Where(badge => badge.IsActive)
                .OrderBy(badge => badge.AwardedAt)
                .Select(badge => badge.ToResponse())
                .ToList();

        return Result.Success(badges);
    }
}
