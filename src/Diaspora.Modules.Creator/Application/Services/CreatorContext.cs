using Diaspora.Modules.Creator.Domain.Creators;
using Diaspora.Modules.Creator.Domain.Errors;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Creator.Application.Services;

/// <summary>
/// The one door to "who is calling, and what may they do to their own creator record".
/// <para>
/// <b>Every write in this module goes through it</b>, so "is this caller allowed" has one answer
/// rather than eleven. That is the same shape B18's <c>CommunityProgrammeAccessService</c> takes and
/// for the same reason: an authorization question answered in eleven places is answered eleven
/// slightly different ways.
/// </para>
/// <para>
/// <b>A creator record is opened on demand and that is deliberate.</b> The Identity registration
/// consumer opens one for every new member, but a platform with existing members predates that
/// consumer — so the first creator-shaped thing anybody does opens their record rather than 404ing.
/// The alternative, a nightly backfill, would leave a window in which a real member had no creator
/// identity and every creator route refused them.
/// </para>
/// </summary>
public sealed class CreatorContext
{
    private readonly ICreatorProfileRepository _creators;
    private readonly ICurrentUser _currentUser;
    private readonly ITrustService _trust;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CreatorContext(
        ICreatorProfileRepository creators,
        ICurrentUser currentUser,
        ITrustService trust,
        IGuidGenerator guids,
        IClock clock)
    {
        _creators = creators;
        _currentUser = currentUser;
        _trust = trust;
        _guids = guids;
        _clock = clock;
    }

    /// <summary>The caller's user identifier, or an unauthorized failure.</summary>
    public Result<Guid> RequireUser() =>
        _currentUser.IsAuthenticated && _currentUser.UserId is { } userId
            ? Result.Success(userId.Value)
            : Result.Failure<Guid>(CreatorErrors.NotAuthenticated);

    /// <summary>
    /// The caller's creator record, opening one if they have none.
    /// <para>
    /// Staged rather than saved — the calling handler owns its unit of work, which is what keeps
    /// DSP4003 able to see that the handler saves.
    /// </para>
    /// </summary>
    public async Task<Result<CreatorProfile>> RequireCreatorAsync(
        CancellationToken cancellationToken = default)
    {
        var caller = RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<CreatorProfile>(caller.Error);
        }

        var creator = await _creators.FindByUserAsync(caller.Value, cancellationToken);

        if (creator is not null)
        {
            return Result.Success(creator);
        }

        var opened = CreatorProfile.Open(
            CreatorId.From(_guids.NewSequentialGuid()),
            caller.Value,
            DisplayNameFromClaims(),
            CountryFromClaims(),
            _clock.UtcNow);

        _creators.Add(opened);

        return Result.Success(opened);
    }

    /// <summary>
    /// The caller's creator record, without opening one.
    /// <para>
    /// For reads that should not have a side effect. §GAP-001's <c>GET /creators/me</c> uses
    /// <see cref="RequireCreatorAsync"/> instead, because a member opening the creator dashboard for
    /// the first time should find a projection rather than a 404 — the register's own
    /// <c>NotACreator</c> status exists to be that answer.
    /// </para>
    /// </summary>
    public async Task<CreatorProfile?> FindCreatorAsync(CancellationToken cancellationToken = default)
    {
        var caller = RequireUser();

        return caller.IsFailure
            ? null
            : await _creators.FindByUserAsync(caller.Value, cancellationToken);
    }

    /// <summary>
    /// Whether this creator may change anything at all.
    /// <para>
    /// <b>Suspended refuses everything; restricted refuses what the platform prohibits.</b> The
    /// register's <c>Restricted</c> status is not a read block — a restricted creator still sees
    /// their dashboard — so the refusal lives on the write and names which write it is.
    /// </para>
    /// </summary>
    public static Result EnsureCanModify(CreatorProfile creator)
    {
        ArgumentNullException.ThrowIfNull(creator);

        if (creator.IsSuspended)
        {
            return Result.Failure(CreatorErrors.CreatorSuspended);
        }

        return creator.IsRestricted
            ? Result.Failure(CreatorErrors.CreatorRestricted)
            : Result.Success();
    }

    /// <summary>
    /// Whether this creator may take part — claim, join, apply, redeem.
    /// <para>
    /// Same rule as <see cref="EnsureCanModify"/> today, and a separate method on purpose: the two
    /// are different questions and a deployment that wanted a restricted creator to keep claiming
    /// what they had already earned should be able to say so in one place.
    /// </para>
    /// </summary>
    public static Result EnsureCanParticipate(CreatorProfile creator) => EnsureCanModify(creator);

    /// <summary>
    /// Whether Trust says this member's identity is verified.
    /// <para>
    /// <b>Asked, never stored.</b> Trust owns the answer and this module owns the consequence — which
    /// is the boundary §19 of the phase brief draws: "Creator Platform asks 'Is this member eligible
    /// for creator verification?'; Trust answers 'Is this person's identity verified?'"
    /// </para>
    /// <para>
    /// <b><c>GetTrustLevelAsync</c> rather than <c>IsPublishingEligibleAsync</c>, and the difference
    /// matters.</b> The publishing question folds account <em>health</em> into its answer — Trust's
    /// <c>CanPublish</c> is "good standing AND verified" — so a verified member under a temporary
    /// restriction would read here as though their identity had never been checked. That is the
    /// wrong sentence to show somebody, and it would send them back to a verification flow they have
    /// already completed. Standing is a separate requirement on this application and a separate
    /// question to Trust, asked through <see cref="StandingAsync"/>.
    /// </para>
    /// </summary>
    public async Task<bool> IsIdentityVerifiedAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        await _trust.GetTrustLevelAsync(
            SharedKernel.Identity.UserId.From(userId), cancellationToken)
        >= Platform.Services.Models.TrustLevel.Verified;

    /// <summary>
    /// Whether the caller has finished onboarding.
    /// <para>
    /// <b>Read from the permission set, and that is the least-coupled signal available.</b>
    /// <c>creator:programme:participate</c> is floored at <see cref="Contracts.Authorization.AccessTier.Limited"/>,
    /// so the kernel grants it exactly when onboarding is complete — asking whether the caller holds
    /// it is asking the platform's own question rather than a second version of it.
    /// </para>
    /// <para>
    /// <b>The tier claim would have been the obvious answer and does not exist.</b>
    /// <c>PlatformClaimTypes.AccessTier</c> is declared in <c>Diaspora.Contracts</c> and
    /// <c>JwtAccessTokenIssuer</c> never emits it — the token carries <c>acs</c>, Identity's own
    /// nine-state access state, whose spelling belongs to the Infrastructure layer. Reading a claim
    /// that is never issued would have made this silently false for everybody, which is the shape of
    /// defect that survives a suite asserting only the happy path. Recorded as an out-of-scope
    /// platform finding in the Wave 6 report.
    /// </para>
    /// </summary>
    public bool IsOnboarded() =>
        _currentUser.Permissions.Contains(CreatorPermissions.Participate, StringComparer.Ordinal);

    /// <summary>
    /// A creator projection for a member who has no record yet, for a read that must not write.
    /// <para>
    /// <b>Transient and never staged.</b> Several reads have to answer "what would this member's
    /// standing be" before anything creator-shaped has happened to them — the verification
    /// requirements and the reward catalogue's locked reasons both do. Opening a real record from a
    /// GET would be a side effect on a route that does not need one, and refusing with a 404 would
    /// be the "creator aggregate does not exist" failure the whole phase is closing.
    /// </para>
    /// </summary>
    public CreatorProfile TransientProfileFor(Guid userId) =>
        CreatorProfile.Open(
            CreatorId.From(_guids.NewSequentialGuid()),
            userId,
            DisplayNameFromClaims(),
            CountryFromClaims(),
            _clock.UtcNow);

    /// <summary>
    /// What Trust says about the account's standing.
    /// <para>
    /// <see cref="AccountStanding.Unknown"/> is a real answer and is treated as "no opinion" rather
    /// than as good standing — the kernel's default is deliberately visibly wrong, and reading it as
    /// good would make it invisibly wrong.
    /// </para>
    /// </summary>
    public async Task<Platform.Services.Models.AccountStanding> StandingAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        await _trust.GetAccountStandingAsync(
            SharedKernel.Identity.UserId.From(userId), cancellationToken);

    private string DisplayNameFromClaims()
    {
        foreach (var key in new[] { "name", "fullName", "given_name" })
        {
            if (_currentUser.Claims.TryGetValue(key, out var value)
                && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return "Member";
    }

    private string? CountryFromClaims()
    {
        foreach (var key in new[] { "country", "countryOfResidence", "country_code" })
        {
            if (_currentUser.Claims.TryGetValue(key, out var value)
                && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
