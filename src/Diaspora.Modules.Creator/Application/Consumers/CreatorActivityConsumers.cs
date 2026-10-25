using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Contracts.CommunityLife.V1.Events;
using Diaspora.Contracts.Guides.V1.Events;
using Diaspora.Contracts.Housing.V1.Events;
using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Contracts.Jobs.V1.Events;
using Diaspora.Contracts.Marketplace.V1.Events;
using Diaspora.Contracts.Providers.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Application.Challenges;
using Diaspora.Modules.Creator.Application.Milestones;
using Diaspora.Modules.Creator.Application.Referrals;
using Diaspora.Modules.Creator.Application.Services;
using Diaspora.Modules.Creator.Domain.Activity;
using Diaspora.Modules.Creator.Domain.Creators;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Modules.Creator.Domain.Rewards;
using Diaspora.Modules.Creator.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Creator.Application.Consumers;

/// <summary>
/// Records one recognised action and everything that follows from it.
/// <para>
/// <b>The single settling path, and every consumer in this file is a thin call into it.</b> DSP1002
/// reports two consumers settling one event; the risk here is subtler — nine different published
/// contracts all mean "this creator did something the programme recognises", and nine copies of what
/// follows would drift. Milestones, challenges, the status ladder and referral qualification are all
/// downstream of one function.
/// </para>
/// <para>
/// <b>Idempotent on the producing event's own subject.</b> Delivery is at-least-once, so a
/// redelivered <c>GuidePublishedIntegrationEvent</c> must not count twice; the source reference is
/// checked here and uniquely indexed at the database, so two workers racing one delivery lose one.
/// </para>
/// <para>
/// <b>A creator record is opened on first activity.</b> A member who published before the creator
/// programme existed gets a record the moment they do something it recognises, which is what makes
/// the leaderboards and the directory reflect the whole platform rather than the people who happened
/// to open the creator tab.
/// </para>
/// </summary>
public sealed class CreatorActivityRecorder
{
    private readonly ICreatorProfileRepository _creators;
    private readonly ICreatorActivityRepository _activity;
    private readonly ICreatorRewardRepository _rewards;
    private readonly MilestoneProgressService _milestones;
    private readonly ChallengeProgressService _challenges;
    private readonly ReferralQualificationService _referrals;
    private readonly CreatorStatusService _status;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CreatorActivityRecorder(
        ICreatorProfileRepository creators,
        ICreatorActivityRepository activity,
        ICreatorRewardRepository rewards,
        MilestoneProgressService milestones,
        ChallengeProgressService challenges,
        ReferralQualificationService referrals,
        CreatorStatusService status,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        IClock clock)
    {
        _creators = creators;
        _activity = activity;
        _rewards = rewards;
        _milestones = milestones;
        _challenges = challenges;
        _referrals = referrals;
        _status = status;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _clock = clock;
    }

    public async Task RecordAsync(
        Guid userId,
        string actionType,
        string sourceReference,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken,
        string? subjectType = null,
        Guid? subjectId = null,
        string? countryCode = null,
        Guid? communityId = null)
    {
        if (userId == Guid.Empty || !CreatorActivityTypes.IsKnown(actionType))
        {
            return;
        }

        var creator = await _creators.FindByUserAsync(userId, cancellationToken);
        var isNewCreator = creator is null;

        creator ??= CreatorProfile.Open(
            CreatorId.From(_guids.NewSequentialGuid()),
            userId,
            "Member",
            countryCode,
            _clock.UtcNow);

        if (isNewCreator)
        {
            _creators.Add(creator);
        }

        // The barrier. Checked before anything is staged, so a redelivery costs one query.
        if (await _activity.ExistsAsync(creator.Id, sourceReference, cancellationToken))
        {
            return;
        }

        // The weight is the rules catalogue's, so what an action is worth to a milestone and a
        // leaderboard is one number rather than two. It pays no points: Trust's rules do that, for
        // confirmed outcomes (RWD-02, D-12).
        var rule = await _rewards.FindRuleForActionAsync(actionType, cancellationToken);
        var weight = rule?.Points ?? 1;

        var entry = CreatorActivityEntry.Record(
            CreatorActivityEntryId.From(_guids.NewSequentialGuid()),
            creator.Id,
            userId,
            actionType,
            weight,
            sourceReference,
            occurredAt,
            subjectType,
            subjectId,
            countryCode ?? creator.CountryCode,
            communityId);

        _activity.Add(entry);

        creator.RecordActivity(actionType, weight, _clock.UtcNow);

        if (!isNewCreator)
        {
            _creators.Update(creator);
        }

        await _milestones.RecordAsync(creator, actionType, 1, cancellationToken);
        await _challenges.RecordAsync(creator, actionType, 1, occurredAt, cancellationToken);

        // A recognised action is what makes an invitee's referral qualify. Asking here rather than on
        // a timer means the referrer is paid when the thing happened.
        await _referrals.TryQualifyAsync(userId, cancellationToken);

        await _status.RefreshAsync(creator, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════
// The consumers.
//
// Nine published contracts, nine thin handlers, one settling path. Each names the action type and
// the producing event's own subject; nothing else about the producing module reaches this assembly.
//
// Every one of them is at-least-once and silently idempotent: a redelivery records nothing and
// answers success, because a consumer that threw on its second delivery would be redelivered for
// ever.
// ════════════════════════════════════════════════════════════════════════════════════════════════

public sealed class GuidePublishedCreatorConsumer
    : IntegrationEventHandler<GuidePublishedIntegrationEvent>
{
    private readonly CreatorActivityRecorder _recorder;

    public GuidePublishedCreatorConsumer(CreatorActivityRecorder recorder) => _recorder = recorder;

    protected override Task HandleAsync(
        GuidePublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // A republication is not a new contribution. IsFirstPublication is B8's own answer to that
        // question, and taking it is better than this module inventing one.
        return integrationEvent.IsFirstPublication
            ? _recorder.RecordAsync(
                integrationEvent.AuthorId,
                CreatorActivityTypes.GuidePublished,
                $"{CreatorActivityTypes.GuidePublished}:{integrationEvent.GuideId:N}",
                integrationEvent.OccurredAt,
                cancellationToken,
                "guide",
                integrationEvent.GuideId,
                integrationEvent.CountryCode)
            : Task.CompletedTask;
    }
}

public sealed class GuideFeaturedCreatorConsumer
    : IntegrationEventHandler<GuideFeaturedIntegrationEvent>
{
    private readonly CreatorActivityRecorder _recorder;
    private readonly IGuidesAuthorLookup _authors;

    public GuideFeaturedCreatorConsumer(
        CreatorActivityRecorder recorder, IGuidesAuthorLookup authors)
    {
        _recorder = recorder;
        _authors = authors;
    }

    protected override async Task HandleAsync(
        GuideFeaturedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // B8's featured contract names the guide but not its author. Rather than reference B8, this
        // module resolves the author from the activity it already recorded when the guide was
        // published — which is the same fact, held here.
        var author = await _authors.AuthorOfAsync(integrationEvent.GuideId, cancellationToken);

        if (author is not { } authorId)
        {
            return;
        }

        await _recorder.RecordAsync(
            authorId,
            CreatorActivityTypes.GuideFeatured,
            $"{CreatorActivityTypes.GuideFeatured}:{integrationEvent.GuideId:N}",
            integrationEvent.OccurredAt,
            cancellationToken,
            "guide",
            integrationEvent.GuideId);
    }
}

/// <summary>
/// Who published a guide, answered from this module's own activity ledger.
/// <para>
/// <b>A seam rather than a reference to B8.</b> The featured contract carries the guide but not the
/// author, and the alternative to this was a project reference from the Creator Platform into the
/// Guides module for a single Guid — which is the shape B2.6 exists to prevent. Enriching B8's
/// contract would be the better long-term fix and is recorded as an external dependency.
/// </para>
/// </summary>
public interface IGuidesAuthorLookup
{
    Task<Guid?> AuthorOfAsync(Guid guideId, CancellationToken cancellationToken = default);
}

/// <summary>Resolves an author from the entry recorded when the guide was published.</summary>
public sealed class ActivityLedgerAuthorLookup : IGuidesAuthorLookup
{
    private readonly Persistence.CreatorActivityLookup _lookup;

    public ActivityLedgerAuthorLookup(Persistence.CreatorActivityLookup lookup) => _lookup = lookup;

    public Task<Guid?> AuthorOfAsync(Guid guideId, CancellationToken cancellationToken = default) =>
        _lookup.UserForSubjectAsync("guide", guideId, cancellationToken);
}

public sealed class PostPublishedCreatorConsumer
    : IntegrationEventHandler<CommunityPostPublishedIntegrationEvent>
{
    private readonly CreatorActivityRecorder _recorder;

    public PostPublishedCreatorConsumer(CreatorActivityRecorder recorder) => _recorder = recorder;

    protected override Task HandleAsync(
        CommunityPostPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return _recorder.RecordAsync(
            integrationEvent.AuthorId,
            CreatorActivityTypes.PostPublished,
            $"{CreatorActivityTypes.PostPublished}:{integrationEvent.PostId:N}",
            integrationEvent.PublishedAt,
            cancellationToken,
            "post",
            integrationEvent.PostId,
            integrationEvent.CountryCode,
            integrationEvent.OrganizationId);
    }
}

public sealed class CommentAddedCreatorConsumer
    : IntegrationEventHandler<CommunityCommentAddedIntegrationEvent>
{
    private readonly CreatorActivityRecorder _recorder;

    public CommentAddedCreatorConsumer(CreatorActivityRecorder recorder) => _recorder = recorder;

    protected override Task HandleAsync(
        CommunityCommentAddedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return _recorder.RecordAsync(
            integrationEvent.AuthorId,
            CreatorActivityTypes.CommentAdded,
            $"{CreatorActivityTypes.CommentAdded}:{integrationEvent.CommentId:N}",
            integrationEvent.OccurredAt,
            cancellationToken,
            "comment",
            integrationEvent.CommentId);
    }
}

public sealed class EventPublishedCreatorConsumer
    : IntegrationEventHandler<EventPublishedIntegrationEvent>
{
    private readonly CreatorActivityRecorder _recorder;

    public EventPublishedCreatorConsumer(CreatorActivityRecorder recorder) => _recorder = recorder;

    protected override Task HandleAsync(
        EventPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return _recorder.RecordAsync(
            integrationEvent.OrganizerUserId,
            CreatorActivityTypes.EventPublished,
            $"{CreatorActivityTypes.EventPublished}:{integrationEvent.CommunityEventId:N}",
            integrationEvent.OccurredAt,
            cancellationToken,
            "event",
            integrationEvent.CommunityEventId,
            communityId: integrationEvent.OrganizationId);
    }
}

public sealed class EventCompletedCreatorConsumer
    : IntegrationEventHandler<EventCompletedIntegrationEvent>
{
    private readonly CreatorActivityRecorder _recorder;

    public EventCompletedCreatorConsumer(CreatorActivityRecorder recorder) => _recorder = recorder;

    protected override Task HandleAsync(
        EventCompletedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return _recorder.RecordAsync(
            integrationEvent.OrganizerUserId,
            CreatorActivityTypes.EventHosted,
            $"{CreatorActivityTypes.EventHosted}:{integrationEvent.CommunityEventId:N}",
            integrationEvent.CompletedAt,
            cancellationToken,
            "event",
            integrationEvent.CommunityEventId,
            integrationEvent.CountryCode,
            integrationEvent.OrganizationId);
    }
}

/// <summary>
/// A marketplace publication, credited to the member who owns the seller profile.
/// <para>
/// <b>Not <c>SellerId</c>, and that is BG-10.</b> This consumer passed the <em>seller profile</em>
/// identifier where <see cref="CreatorActivityRecorder.RecordAsync"/> expects a member, and the
/// recorder opens a creator record for whatever it is handed — so every marketplace publication
/// minted a creator profile keyed to a profile id that is not a person, the publishing member was
/// never credited, and leaderboards, milestones and reward points were all computed over the wrong
/// subject. The correcting migration is <c>RepairCreatorActivityAttribution</c>.
/// </para>
/// <para>
/// <b>The contract already carried the answer.</b> <c>SellerUserId</c> was added in Wave 12 with a
/// doc comment describing this exact failure mode for a different consumer. Nothing needed to be
/// published that was not already being published; this consumer was reading the wrong field.
/// </para>
/// </summary>
public sealed class ListingPublishedCreatorConsumer
    : IntegrationEventHandler<ListingPublishedIntegrationEvent>
{
    private readonly CreatorActivityRecorder _recorder;

    public ListingPublishedCreatorConsumer(CreatorActivityRecorder recorder) => _recorder = recorder;

    protected override Task HandleAsync(
        ListingPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // Nullable, because it is absent on outbox rows written before Wave 12. Recording nothing is
        // the correct answer and falling back to SellerId is not: the fallback is the defect, and it
        // fails by writing a wrong row rather than by writing none.
        return integrationEvent.SellerUserId is { } sellerUserId
            ? _recorder.RecordAsync(
                sellerUserId,
                CreatorActivityTypes.ListingPublished,
                $"{CreatorActivityTypes.ListingPublished}:{integrationEvent.ListingId:N}",
                integrationEvent.OccurredAt,
                cancellationToken,
                "listing",
                integrationEvent.ListingId,
                integrationEvent.CountryCode)
            : Task.CompletedTask;
    }
}

public sealed class JobPublishedCreatorConsumer
    : IntegrationEventHandler<JobPublishedIntegrationEvent>
{
    private readonly CreatorActivityRecorder _recorder;

    public JobPublishedCreatorConsumer(CreatorActivityRecorder recorder) => _recorder = recorder;

    protected override Task HandleAsync(
        JobPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // See ListingPublishedCreatorConsumer. This passed EmployerId — the organisation — for the
        // same reason and with the same consequence.
        return integrationEvent.EmployerUserId is { } employerUserId
            ? _recorder.RecordAsync(
                employerUserId,
                CreatorActivityTypes.JobPublished,
                $"{CreatorActivityTypes.JobPublished}:{integrationEvent.JobId:N}",
                integrationEvent.OccurredAt,
                cancellationToken,
                "job",
                integrationEvent.JobId,
                integrationEvent.CountryCode)
            : Task.CompletedTask;
    }
}

public sealed class HousingPublishedCreatorConsumer
    : IntegrationEventHandler<HousingPublishedIntegrationEvent>
{
    private readonly CreatorActivityRecorder _recorder;

    public HousingPublishedCreatorConsumer(CreatorActivityRecorder recorder) => _recorder = recorder;

    protected override Task HandleAsync(
        HousingPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // See ListingPublishedCreatorConsumer. This passed LandlordId — the profile — for the same
        // reason and with the same consequence. Housing was not named in BG-10's original write-up;
        // it was found by asking the database which action types resolved to a member, and this was
        // the third.
        return integrationEvent.LandlordUserId is { } landlordUserId
            ? _recorder.RecordAsync(
                landlordUserId,
                CreatorActivityTypes.HousingPublished,
                $"{CreatorActivityTypes.HousingPublished}:{integrationEvent.ListingId:N}",
                integrationEvent.OccurredAt,
                cancellationToken,
                "housing",
                integrationEvent.ListingId,
                integrationEvent.CountryCode)
            : Task.CompletedTask;
    }
}

public sealed class ProviderPublishedCreatorConsumer
    : IntegrationEventHandler<ProviderPublishedIntegrationEvent>
{
    private readonly CreatorActivityRecorder _recorder;

    public ProviderPublishedCreatorConsumer(CreatorActivityRecorder recorder) => _recorder = recorder;

    protected override Task HandleAsync(
        ProviderPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return _recorder.RecordAsync(
            integrationEvent.OwnerId,
            CreatorActivityTypes.ProviderPublished,
            $"{CreatorActivityTypes.ProviderPublished}:{integrationEvent.ProviderId:N}",
            integrationEvent.PublishedAt,
            cancellationToken,
            "provider",
            integrationEvent.ProviderId,
            integrationEvent.CountryCode);
    }
}

/// <summary>
/// Keeps the follower counter that the status ladder and the public profile read.
/// <para>
/// <b>A counter rather than a query into B6.</b> The register's status rules need a follower figure
/// on every read; joining to another module's follow table on a directory page would be a
/// cross-module query in the plan. B6 publishes the fact and this module keeps the tally.
/// </para>
/// </summary>
public sealed class FollowCreatedCreatorConsumer
    : IntegrationEventHandler<CommunityFollowCreatedIntegrationEvent>
{
    private readonly ICreatorProfileRepository _creators;
    private readonly CreatorStatusService _status;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public FollowCreatedCreatorConsumer(
        ICreatorProfileRepository creators,
        CreatorStatusService status,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _creators = creators;
        _status = status;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    protected override async Task HandleAsync(
        CommunityFollowCreatedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // Community publishes "member". This compared against "User" from the start, so no follow
        // ever reached a creator's count until 20 Oct 2026. "User" is still accepted for any event
        // written in the old vocabulary.
        if (!FollowTargets.IsMember(integrationEvent.TargetType))
        {
            return;
        }

        var followed = await _creators.FindByUserAsync(integrationEvent.TargetId, cancellationToken);

        if (followed is null)
        {
            // Nobody has a creator record yet. The follower count will be right from the first
            // recognised action, which is when the record opens — and a member with no contributions
            // is not on a leaderboard for a follower count to matter to.
            return;
        }

        followed.AdjustAudience(1, 0, 0, _clock.UtcNow);
        _creators.Update(followed);

        var follower = await _creators.FindByUserAsync(
            integrationEvent.FollowerId, cancellationToken);

        if (follower is not null)
        {
            follower.AdjustAudience(0, 1, 0, _clock.UtcNow);
            _creators.Update(follower);
        }

        await _status.RefreshAsync(followed, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// The other half of <see cref="FollowCreatedCreatorConsumer"/>: an unfollow takes one away.
/// <para>
/// Never below zero — <c>AdjustAudience</c> clamps — so an unfollow of a follow that was counted
/// before this consumer existed (or never counted, under the old "User" comparison) cannot drive a
/// tally negative.
/// </para>
/// </summary>
public sealed class FollowRemovedCreatorConsumer
    : IntegrationEventHandler<CommunityFollowRemovedIntegrationEvent>
{
    private readonly ICreatorProfileRepository _creators;
    private readonly CreatorStatusService _status;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public FollowRemovedCreatorConsumer(
        ICreatorProfileRepository creators,
        CreatorStatusService status,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _creators = creators;
        _status = status;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    protected override async Task HandleAsync(
        CommunityFollowRemovedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (!FollowTargets.IsMember(integrationEvent.TargetType))
        {
            return;
        }

        var followed = await _creators.FindByUserAsync(integrationEvent.TargetId, cancellationToken);

        if (followed is null)
        {
            return;
        }

        followed.AdjustAudience(-1, 0, 0, _clock.UtcNow);
        _creators.Update(followed);

        var follower = await _creators.FindByUserAsync(
            integrationEvent.FollowerId, cancellationToken);

        if (follower is not null)
        {
            follower.AdjustAudience(0, -1, 0, _clock.UtcNow);
            _creators.Update(follower);
        }

        await _status.RefreshAsync(followed, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Which follow events concern a member, in either vocabulary Community has used.</summary>
internal static class FollowTargets
{
    public static bool IsMember(string? targetType) =>
        string.Equals(targetType, "member", StringComparison.OrdinalIgnoreCase)
        || string.Equals(targetType, "User", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Opens a creator record and a sign-up bonus when somebody joins.
/// <para>
/// <b>This is what makes <c>NotACreator</c> a state rather than a missing row</b>, and it is what
/// gives CREATOR-GAP-010's bonus somewhere to live from the member's first second on the platform.
/// </para>
/// <para>
/// Idempotent: a redelivered registration finds the record and does nothing.
/// </para>
/// </summary>
public sealed class UserRegisteredCreatorConsumer
    : IntegrationEventHandler<UserRegisteredIntegrationEvent>
{
    private readonly ICreatorProfileRepository _creators;
    private readonly ISignupBonusRepository _bonuses;
    private readonly ReferralQualificationService _referrals;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOptions<CreatorOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public UserRegisteredCreatorConsumer(
        ICreatorProfileRepository creators,
        ISignupBonusRepository bonuses,
        ReferralQualificationService referrals,
        IUnitOfWork unitOfWork,
        IOptions<CreatorOptions> options,
        IGuidGenerator guids,
        IClock clock)
    {
        _creators = creators;
        _bonuses = bonuses;
        _referrals = referrals;
        _unitOfWork = unitOfWork;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    protected override async Task HandleAsync(
        UserRegisteredIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var now = _clock.UtcNow;
        var creator = await _creators.FindByUserAsync(integrationEvent.UserId, cancellationToken);

        if (creator is null)
        {
            creator = CreatorProfile.Open(
                CreatorId.From(_guids.NewSequentialGuid()),
                integrationEvent.UserId,
                integrationEvent.FullName,
                integrationEvent.CountryOfResidence,
                now);

            _creators.Add(creator);
        }
        else
        {
            creator.RefreshIdentity(
                integrationEvent.FullName,
                integrationEvent.CountryOfResidence,
                null,
                null,
                now);

            _creators.Update(creator);
        }

        var existing = await _bonuses.FindForUserAsync(integrationEvent.UserId, cancellationToken);

        if (existing is null)
        {
            _bonuses.Add(SignupBonus.Offer(
                SignupBonusId.From(_guids.NewSequentialGuid()),
                integrationEvent.UserId,
                creator.Id,
                _options.Value.SignupBonusPoints,
                now,
                now.AddDays(_options.Value.SignupBonusValidDays)));
        }

        // A registration through an invitation (RWD-03). Every refusal is the same silence: an
        // unknown code, the member's own, an invitee already referred. The referrer sees the invitee
        // by first name only.
        if (!string.IsNullOrWhiteSpace(integrationEvent.ReferralCode))
        {
            await _referrals.AttributeSignupAsync(
                integrationEvent.ReferralCode,
                integrationEvent.UserId,
                FirstName(integrationEvent.FullName),
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string FirstName(string fullName) =>
        fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;
}

/// <summary>
/// Records a qualified referral as the referrer's activity, so the <c>first-referral</c> milestone
/// can be reached (RWD-11).
/// <para>
/// This module's own event. The points are Trust's to pay (D-12); this records the fact for the
/// programme's milestones and leaderboards, idempotent on the referral.
/// </para>
/// </summary>
public sealed class ReferralQualifiedCreatorConsumer
    : IntegrationEventHandler<Diaspora.Contracts.Creator.V1.Events.CreatorReferralQualifiedIntegrationEvent>
{
    private readonly CreatorActivityRecorder _recorder;

    public ReferralQualifiedCreatorConsumer(CreatorActivityRecorder recorder) => _recorder = recorder;

    protected override Task HandleAsync(
        Diaspora.Contracts.Creator.V1.Events.CreatorReferralQualifiedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return _recorder.RecordAsync(
            integrationEvent.ReferrerUserId,
            CreatorActivityTypes.ReferralQualified,
            $"{CreatorActivityTypes.ReferralQualified}:{integrationEvent.ReferralId:N}",
            integrationEvent.QualifiedAt,
            cancellationToken,
            "referral",
            integrationEvent.ReferralId);
    }
}
