using System.Security.Cryptography;
using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Application.Mapping;
using Diaspora.Modules.Creator.Application.Services;
using Diaspora.Modules.Creator.Contracts.V1.Requests;
using Diaspora.Modules.Creator.Contracts.V1.Responses;
using Diaspora.Modules.Creator.Domain.Creators;
using Diaspora.Modules.Creator.Domain.Errors;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.Modules.Creator.Domain.Referrals;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Modules.Creator.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Creator.Application.Referrals;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// CREATOR-GAP-009 — the referral programme.
//
// Three routes. The register's central point is that four states stay distinct: "a client counting
// signups and promising a reward would be promising money the platform never agreed to pay."
//
// Qualification is ReferralQualificationPolicy's, checked against the recognised-activity ledger,
// and the reward is a points grant published to B15 — never a credit written here.
//
// The anti-abuse checks are listed in the policy and enforced in ReferralQualificationService. What
// a member is told when one refuses is a single sentence that names nothing.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Issues codes and decides qualification.
/// <para>
/// <b>One class for both, because the two are the same rule seen from two ends.</b> A code is what
/// attributes a signup; qualification is what makes that signup worth something. Splitting them
/// would put the anti-abuse checks in one place and the attribution in another, which is how a
/// self-referral gets through.
/// </para>
/// </summary>
public sealed class ReferralQualificationService
{
    /// <summary>
    /// The alphabet a code is drawn from.
    /// <para>
    /// No <c>0</c>, <c>O</c>, <c>1</c>, <c>I</c> or <c>L</c>: a code is read down a phone and typed
    /// by somebody else, and an ambiguous character costs a referral rather than a keystroke.
    /// </para>
    /// </summary>
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    private readonly ICreatorReferralRepository _referrals;
    private readonly ICreatorActivityRepository _activity;
    private readonly ICreatorProfileRepository _creators;
    private readonly ILogger<ReferralQualificationService> _logger;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public ReferralQualificationService(
        ICreatorReferralRepository referrals,
        ICreatorActivityRepository activity,
        ICreatorProfileRepository creators,
        ILogger<ReferralQualificationService> logger,
        IGuidGenerator guids,
        IClock clock)
    {
        _referrals = referrals;
        _activity = activity;
        _creators = creators;
        _logger = logger;
        _guids = guids;
        _clock = clock;
    }

    /// <summary>
    /// This creator's code, issuing one if they have none.
    /// <para>
    /// <b>Idempotent on the creator.</b> A second call returns the first code rather than minting
    /// another — which is what §12 of the phase brief asks for and what makes the route safe to
    /// retry. Stages; the caller saves.
    /// </para>
    /// </summary>
    public async Task<CreatorReferralCode> EnsureCodeAsync(
        CreatorProfile creator, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creator);

        var existing = await _referrals.FindCodeForCreatorAsync(creator.Id, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var issued = CreatorReferralCode.Issue(
            CreatorReferralCodeId.From(_guids.NewSequentialGuid()),
            creator.Id,
            creator.UserId,
            await GenerateUniqueCodeAsync(cancellationToken),
            _clock.UtcNow);

        _referrals.AddCode(issued);

        return issued;
    }

    /// <summary>
    /// Attributes a registration to a code.
    /// <para>
    /// <b>Every anti-abuse check the programme has is here, and each returns the same silence.</b>
    /// Self-referral, an invitee already referred by somebody else, and an unknown or inactive code
    /// all produce no referral row and a log line — never a message that tells the caller which check
    /// refused them. Stages; the caller saves.
    /// </para>
    /// </summary>
    public async Task<CreatorReferral?> AttributeSignupAsync(
        string code,
        Guid inviteeUserId,
        string inviteeLabel,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var issued = await _referrals.FindCodeAsync(code, cancellationToken);

        if (issued is null)
        {
            _logger.LogInformation(
                "Referral attribution refused: unknown or inactive code.");

            return null;
        }

        if (issued.UserId == inviteeUserId)
        {
            _logger.LogWarning(
                "Referral attribution refused: self-referral by creator {CreatorId}.",
                issued.CreatorId.Value);

            return null;
        }

        var already = await _referrals.FindByInviteeAsync(inviteeUserId, cancellationToken);

        if (already is not null)
        {
            _logger.LogInformation(
                "Referral attribution refused: invitee already referred.");

            return null;
        }

        var referral = CreatorReferral.Invite(
            CreatorReferralId.From(_guids.NewSequentialGuid()), issued, _clock.UtcNow);

        var recorded = referral.RecordSignup(inviteeUserId, inviteeLabel, _clock.UtcNow);

        if (recorded.IsFailure)
        {
            return null;
        }

        referral.MarkPending(ReferralQualificationPolicy.PendingReason, _clock.UtcNow);

        _referrals.AddReferral(referral);

        return referral;
    }

    /// <summary>
    /// Re-checks whether an invitee's referral now qualifies.
    /// <para>
    /// <b>The rule is what the invitee <em>did</em>, not that they exist.</b> A referral programme
    /// that paid on registration pays for registrations. Stages; the caller saves.
    /// </para>
    /// </summary>
    public async Task<bool> TryQualifyAsync(
        Guid inviteeUserId, CancellationToken cancellationToken = default)
    {
        var referral = await _referrals.FindByInviteeAsync(inviteeUserId, cancellationToken);

        if (referral is null)
        {
            return false;
        }

        var now = _clock.UtcNow;

        if (referral.HasLapsed(now))
        {
            referral.Expire(now);
            _referrals.UpdateReferral(referral);

            return false;
        }

        if (referral.State is not (Domain.Enums.ReferralState.SignedUp
            or Domain.Enums.ReferralState.Pending))
        {
            return false;
        }

        var invitee = await _creators.FindByUserAsync(inviteeUserId, cancellationToken);

        if (invitee is null)
        {
            return false;
        }

        // A suspended or restricted invitee does not qualify a referral. The referrer is told the
        // referral did not qualify and nothing about why.
        if (invitee.IsSuspended || invitee.IsRestricted)
        {
            _logger.LogWarning(
                "Referral qualification refused: invitee standing. Referral {ReferralId}.",
                referral.Id.Value);

            referral.Reject(ReferralQualificationPolicy.DeclinedReason, now);
            _referrals.UpdateReferral(referral);

            return false;
        }

        var actions = await _activity.CountForCreatorAsync(
            invitee.Id, referral.SignedUpAt, cancellationToken);

        if (actions < ReferralQualificationPolicy.RequiredRecognisedActions)
        {
            referral.MarkPending(ReferralQualificationPolicy.PendingReason, now);
            _referrals.UpdateReferral(referral);

            return false;
        }

        var reference = $"referral:{referral.Id.Value:N}";

        var qualified = referral.Qualify(
            ReferralQualificationPolicy.RewardPoints, reference, now);

        if (qualified.IsFailure)
        {
            return false;
        }

        referral.MarkRewarded(now);

        _referrals.UpdateReferral(referral);

        return true;
    }

    private async Task<string> GenerateUniqueCodeAsync(CancellationToken cancellationToken)
    {
        // Ten attempts is far more than a 2.8×10^11 space needs; the loop exists so a collision is a
        // retry rather than a 500, and the bound exists so a broken generator is not an infinite one.
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidate = string.Create(
                CreatorLimits.ReferralCodeLength,
                0,
                (span, _) =>
                {
                    for (var index = 0; index < span.Length; index++)
                    {
                        span[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
                    }
                });

            if (await _referrals.FindCodeAsync(candidate, cancellationToken) is null)
            {
                return candidate;
            }
        }

        // Falls back to a value the unique index will still protect. Reaching here means the
        // generator or the space is wrong, and the index failing the insert is the right next step.
        return Guid.NewGuid().ToString("N")[..CreatorLimits.ReferralCodeLength].ToUpperInvariant();
    }
}

/// <summary>
/// The caller's referral programme.
/// <para>
/// Issues a code on first read, for the same reason the creator record is opened on first read: a
/// screen whose whole purpose is to show somebody their code should not ask them to press a button
/// first.
/// </para>
/// </summary>
[Authorize(Permission = CreatorPermissions.ProfileRead)]
[SideEffecting(Reason =
    "Issues the caller's referral code on first read. Idempotent on the creator, so a second read "
    + "returns the first code.")]
public sealed record GetMyReferralProgrammeQuery : IQuery<CreatorReferralProgrammeResponse>;

public sealed class GetMyReferralProgrammeQueryHandler
    : IQueryHandler<GetMyReferralProgrammeQuery, CreatorReferralProgrammeResponse>
{
    private readonly CreatorContext _context;
    private readonly ReferralQualificationService _referralService;
    private readonly ICreatorReferralRepository _referrals;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOptions<CreatorOptions> _options;
    private readonly IClock _clock;

    public GetMyReferralProgrammeQueryHandler(
        CreatorContext context,
        ReferralQualificationService referralService,
        ICreatorReferralRepository referrals,
        IUnitOfWork unitOfWork,
        IOptions<CreatorOptions> options,
        IClock clock)
    {
        _context = context;
        _referralService = referralService;
        _referrals = referrals;
        _unitOfWork = unitOfWork;
        _options = options;
        _clock = clock;
    }

    public async Task<Result<CreatorReferralProgrammeResponse>> Handle(
        GetMyReferralProgrammeQuery query, CancellationToken cancellationToken)
    {
        var creator = await _context.RequireCreatorAsync(cancellationToken);

        if (creator.IsFailure)
        {
            return Result.Failure<CreatorReferralProgrammeResponse>(creator.Error);
        }

        var code = await _referralService.EnsureCodeAsync(creator.Value, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var counts = await _referrals.CountsForCreatorAsync(creator.Value.Id, cancellationToken);

        var eligible = !creator.Value.IsSuspended && !creator.Value.IsRestricted;

        return Result.Success(new CreatorReferralProgrammeResponse
        {
            Code = code.Code,

            // Built by the server so the domain is never guessed by a client — the register's own
            // requirement, and the reason the base URL is configuration rather than a constant.
            ShareUrl = $"{_options.Value.ReferralShareBaseUrl.TrimEnd('/')}/{code.Code}",
            RewardDescription =
                $"{ReferralQualificationPolicy.RewardPoints} points for every referral that qualifies.",
            Terms = ReferralQualificationPolicy.Terms,
            QualificationDescription = ReferralQualificationPolicy.Description,
            QualificationVersion = ReferralQualificationPolicy.Version,
            Invited = counts.Invited,
            SignedUp = counts.SignedUp,
            Qualified = counts.Qualified,
            PendingRewards = counts.PendingRewards,
            ConfirmedRewards = counts.Rewarded,
            TotalPointsEarned = counts.PointsEarned,
            IsEligible = eligible,
            IneligibleReason = eligible
                ? null
                : "The referral programme is not available on your account at the moment.",
            UpdatedAt = _clock.UtcNow
        });
    }
}

/// <summary>The caller's referral history, cursor-paged.</summary>
[Authorize(Permission = CreatorPermissions.ProfileRead)]
public sealed record GetMyReferralHistoryQuery : IQuery<CreatorReferralPageResponse>
{
    public string? Cursor { get; init; }

    public int? Take { get; init; }
}

public sealed class GetMyReferralHistoryQueryHandler
    : IQueryHandler<GetMyReferralHistoryQuery, CreatorReferralPageResponse>
{
    private readonly CreatorContext _context;
    private readonly ICreatorReferralRepository _referrals;

    public GetMyReferralHistoryQueryHandler(
        CreatorContext context, ICreatorReferralRepository referrals)
    {
        _context = context;
        _referrals = referrals;
    }

    public async Task<Result<CreatorReferralPageResponse>> Handle(
        GetMyReferralHistoryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<CreatorReferralPageResponse>(caller.Error);
        }

        var creator = await _context.FindCreatorAsync(cancellationToken);

        if (creator is null)
        {
            return Result.Success(new CreatorReferralPageResponse
            {
                Referrals = [],
                NextCursor = null,
                HasMore = false
            });
        }

        var page = await _referrals.ListForCreatorAsync(
            creator.Id, query.Cursor, CreatorLimits.PageSize(query.Take), cancellationToken);

        return Result.Success(new CreatorReferralPageResponse
        {
            Referrals = page.Referrals.Select(referral => referral.ToResponse()).ToList(),
            NextCursor = page.NextCursor,
            HasMore = page.HasMore
        });
    }
}

/// <summary>Issues a code where none exists. Idempotent on the creator.</summary>
[Authorize(Permission = CreatorPermissions.Participate)]
[Auditable(Action = "creator.referral.code")]
public sealed record IssueReferralCodeCommand : ICommand<CreatorReferralProgrammeResponse>
{
    public IssueReferralCodeRequest? Request { get; init; }
}

public sealed class IssueReferralCodeCommandHandler
    : ICommandHandler<IssueReferralCodeCommand, CreatorReferralProgrammeResponse>
{
    private readonly CreatorContext _context;
    private readonly ReferralQualificationService _referralService;
    private readonly ICreatorReferralRepository _referrals;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOptions<CreatorOptions> _options;
    private readonly IClock _clock;

    public IssueReferralCodeCommandHandler(
        CreatorContext context,
        ReferralQualificationService referralService,
        ICreatorReferralRepository referrals,
        IUnitOfWork unitOfWork,
        IOptions<CreatorOptions> options,
        IClock clock)
    {
        _context = context;
        _referralService = referralService;
        _referrals = referrals;
        _unitOfWork = unitOfWork;
        _options = options;
        _clock = clock;
    }

    public async Task<Result<CreatorReferralProgrammeResponse>> Handle(
        IssueReferralCodeCommand command, CancellationToken cancellationToken)
    {
        var creator = await _context.RequireCreatorAsync(cancellationToken);

        if (creator.IsFailure)
        {
            return Result.Failure<CreatorReferralProgrammeResponse>(creator.Error);
        }

        var allowed = CreatorContext.EnsureCanParticipate(creator.Value);

        if (allowed.IsFailure)
        {
            return Result.Failure<CreatorReferralProgrammeResponse>(allowed.Error);
        }

        var code = await _referralService.EnsureCodeAsync(creator.Value, cancellationToken);

        // The unique index on the creator is the barrier: two concurrent issues produce one code,
        // and the loser replays the winner's.
        var commit = await CreatorIdempotency.GuardAsync(
            code.Code,
            token => _unitOfWork.SaveChangesAsync(token),
            token => _referrals.FindCodeForCreatorAsync(creator.Value.Id, token),
            cancellationToken);

        if (commit.IsFailure)
        {
            return Result.Failure<CreatorReferralProgrammeResponse>(commit.Error);
        }

        var issued = commit.Value.Replayed ?? code;
        var counts = await _referrals.CountsForCreatorAsync(creator.Value.Id, cancellationToken);

        return Result.Success(new CreatorReferralProgrammeResponse
        {
            Code = issued.Code,
            ShareUrl = $"{_options.Value.ReferralShareBaseUrl.TrimEnd('/')}/{issued.Code}",
            RewardDescription =
                $"{ReferralQualificationPolicy.RewardPoints} points for every referral that qualifies.",
            Terms = ReferralQualificationPolicy.Terms,
            QualificationDescription = ReferralQualificationPolicy.Description,
            QualificationVersion = ReferralQualificationPolicy.Version,
            Invited = counts.Invited,
            SignedUp = counts.SignedUp,
            Qualified = counts.Qualified,
            PendingRewards = counts.PendingRewards,
            ConfirmedRewards = counts.Rewarded,
            TotalPointsEarned = counts.PointsEarned,
            IsEligible = true,
            IneligibleReason = null,
            UpdatedAt = _clock.UtcNow
        });
    }
}
