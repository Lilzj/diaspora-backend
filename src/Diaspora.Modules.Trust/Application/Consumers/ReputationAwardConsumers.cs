using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Contracts.Guides.V1.Events;
using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Contracts.Providers.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Domain;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Trust.Application.Consumers;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// THE SCORING TABLE HAD EIGHTEEN RULES AND TEN OF THEM HAD NO PRODUCER
//
// `ReputationScoring` declares what every action on this platform is worth — publishing a guide,
// writing a comment, receiving a helpful vote, eighteen of them, with points, caps and cooldowns.
// Three reached `ScoreAsync`: CommunityConfirmation, ViolationRecorded and ContentRemoved. Two of
// those three are penalties, so the only reliable way to move a member's reputation was to lose
// it.
//
// Five more reached `ApplyAwardAsync` instead, from `ReviewVerification`, and are covered below
// under WHAT WAS ALREADY PAID. Counting only the `ScoreAsync` callers is how two duplicate
// consumers came to be written for this file — the second producer is easy to miss when the two
// paths into the same ledger have different names.
//
// That leaves ten rules with no producer at all: everything a member does that is neither a
// verification nor a punishment. Publishing a guide, writing a comment, being voted helpful.
//
// Nothing was broken in the usual sense. The rules were right, the service that applies them was
// right, the profile that holds the result was right. Nothing called them. The same shape as the
// job adverts nobody could report, the provider carousel nobody could fill, and the country
// standing nobody could close: correct code with no producer.
//
// These consumers are the producers. Each one listens to an event another module already
// publishes and pays the person it names. They are deliberately small and separate rather than a
// switch over one event stream: a module that stops publishing should silently stop paying for
// its own actions, not take the others down with it.
//
// WHAT IS STILL NOT PAID, AND WHY
//
//   * `EmailVerified` — no longer unpaid. Email verification shipped on 5 Oct 2026 and, since
//     20 Oct 2026, `EmailVerificationMirrorConsumer` submits an Email verification that
//     auto-approves, so the award is `EmailVerificationHandler.ApprovalAward`, paid once by the
//     approval. A consumer here that also scored it would be the fourth duplicate producer.
//   * `ProfileCompleted` — retired (D-11). Nothing paid it, and the email and the photo it would
//     have rewarded pay their own awards. The rule is gone; the action name stays for history.
//   * `ProfilePhotoAdded` and `UpdateVerified` — no longer unpaid, since 23 Oct 2026 (REP-13):
//     `ProfilePhotoReputationConsumer` pays the first photo off `UserAvatarChangedIntegrationEvent`,
//     and `PostVerifiedReputationConsumer` pays a post's community verification now that
//     `CommunityPostVerificationChangedIntegrationEvent` names the author.
//   * `ReportResolved` — whose points these are is a product question, not a wiring one. The
//     reporter whose report was upheld is the obvious answer and the moderation event does not
//     name them.
//
// WHAT WAS ALREADY PAID, AND WAS ALMOST PAID TWICE
//
// The approval awards were never missing. Every `IVerificationTypeHandler` carries an
// `ApprovalAward`, and `ReviewVerification` applies it the moment a reviewer approves — so
// GovernmentIdVerified, CountryVerified and ProviderVerified have had a producer all along.
//
// Three consumers written for this file duplicated them and were removed before they shipped.
// One listened for Trust's own `VerificationApprovedDomainEvent`; one for
// `ProviderVerificationChangedIntegrationEvent`, which Providers raises when
// `TrustVerificationProjector` applies the outcome of the very approval that had just paid; and
// one paid `PhoneVerified` off an access-state transition, which became a duplicate the moment
// `PhoneVerificationMirrorConsumer` began submitting a phone verification that auto-approves.
// Neither was caught by the tests that were supposed to cover them, because a member who has
// been paid twice has still been paid: only counting the rows found it. A country approval
// produced two CountryVerified events, 30 points each, against one verification id.
//
// `ScoreAsync` now deduplicates on a `ReputationAwardKey`, which every caller must supply: a
// redelivered event, a retried message or a vote flipped back pays once. That does not make a
// second *producer* safe — two producers that name the same fact differently (a verification id
// here, an event id there) are two keys and two payments. Before adding a consumer here, check
// `VerificationTypeHandlers.cs` for the same action.
//
// The reason production showed nothing despite an approved verification is narrower than it
// looked: that account was approved by hand in SQL during the admin bootstrap, so
// `ReviewVerification` never ran for it.
// ════════════════════════════════════════════════════════════════════════════════════════════════


/// <summary>Pays an author when their guide is published, updated, approved, or voted helpful.</summary>
public sealed class GuideReputationConsumer
    : IntegrationEventHandler<GuidePublishedIntegrationEvent>
{
    private readonly ReputationService _reputation;
    private readonly TrustProfileProvisioner _provisioner;
    private readonly IUnitOfWork _unitOfWork;

    public GuideReputationConsumer(
        ReputationService reputation,
        TrustProfileProvisioner provisioner,
        IUnitOfWork unitOfWork)
    {
        _reputation = reputation;
        _provisioner = provisioner;
        _unitOfWork = unitOfWork;
    }

    protected override async Task HandleAsync(
        GuidePublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // Guides raises this on every publish, new versions included. Only the first is a new
        // guide. The award key alone would catch republishing too, but not for a guide first
        // published before awards were keyed — its original row carries no key to find.
        if (!integrationEvent.IsFirstPublication)
        {
            return;
        }

        var authorId = UserId.From(integrationEvent.AuthorId);

        await _provisioner.GetOrCreateBothAsync(authorId, cancellationToken);

        await _reputation.ScoreAsync(
            authorId,
            ReputationActions.GuidePublished,
            ReputationAwardKey.GuidePublished(integrationEvent.GuideId),
            "guide",
            integrationEvent.GuideId.ToString(),
            integrationEvent.CountryCode,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Pays an author for updating their own guide while others can read it.
/// <para>
/// Not for a draft edit, and not for somebody else's edit of it: both used to pay the author.
/// Whether an edit was substantive enough to count is still to be defined (D-2), so a published
/// typo fix pays like a rewrite.
/// </para>
/// <para>
/// The rule carries its own cap and cooldown, so a guide edited fifty times in an afternoon pays
/// what the scoring table says it pays and no more. That is the table's job, not this consumer's.
/// </para>
/// </summary>
public sealed class GuideUpdatedReputationConsumer
    : IntegrationEventHandler<GuideUpdatedIntegrationEvent>
{
    private readonly ReputationService _reputation;
    private readonly TrustProfileProvisioner _provisioner;
    private readonly IUnitOfWork _unitOfWork;

    public GuideUpdatedReputationConsumer(
        ReputationService reputation,
        TrustProfileProvisioner provisioner,
        IUnitOfWork unitOfWork)
    {
        _reputation = reputation;
        _provisioner = provisioner;
        _unitOfWork = unitOfWork;
    }

    protected override async Task HandleAsync(
        GuideUpdatedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // The author's own edit of a guide others can read, and nothing else (REP-10). A draft edit
        // keeps nobody's guide current, and the event fires for those too. An edit by a contributor
        // or a moderator is not the author's work, and one a projection made names nobody. Whether
        // the edit was substantive is not decided yet (D-2).
        if (!integrationEvent.IsReadableByOthers
            || integrationEvent.AuthorUserId is not { } author
            || integrationEvent.ChangedBy != author)
        {
            return;
        }

        var authorId = UserId.From(author);

        await _provisioner.GetOrCreateBothAsync(authorId, cancellationToken);

        await _reputation.ScoreAsync(
            authorId,
            ReputationActions.GuideUpdated,
            ReputationAwardKey.GuideUpdated(integrationEvent.EventId),
            "guide",
            integrationEvent.GuideId.ToString(),
            cancellationToken: cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Pays an author when their guide is verified.
/// <para>
/// The event fires on every change of verification state, including losing it. Only a move into
/// a verified state pays, and the award key — one per guide — means a guide that flickers does
/// not pay twice. (This comment used to credit a single-occurrence cap the rule never had.)
/// </para>
/// </summary>
public sealed class GuideVerifiedReputationConsumer
    : IntegrationEventHandler<GuideVerificationChangedIntegrationEvent>
{
    private const string Verified = "Verified";

    private readonly ReputationService _reputation;
    private readonly TrustProfileProvisioner _provisioner;
    private readonly IUnitOfWork _unitOfWork;

    public GuideVerifiedReputationConsumer(
        ReputationService reputation,
        TrustProfileProvisioner provisioner,
        IUnitOfWork unitOfWork)
    {
        _reputation = reputation;
        _provisioner = provisioner;
        _unitOfWork = unitOfWork;
    }

    protected override async Task HandleAsync(
        GuideVerificationChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.AuthorUserId is not { } author
            || !string.Equals(integrationEvent.State, Verified, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var authorId = UserId.From(author);

        await _provisioner.GetOrCreateBothAsync(authorId, cancellationToken);

        await _reputation.ScoreAsync(
            authorId,
            ReputationActions.GuideApproved,
            ReputationAwardKey.GuideApproved(integrationEvent.GuideId),
            "guide",
            integrationEvent.GuideId.ToString(),
            cancellationToken: cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Pays an author when somebody votes their guide helpful.
/// <para>
/// <b>The author, never the voter.</b> The event names both, and paying the wrong one would turn
/// a quality signal into a way to farm points by voting. A vote that is not helpful pays nobody,
/// and a member cannot pay themselves.
/// </para>
/// <para>
/// The event also fires when a vote is <em>changed</em>, so Helpful → Not helpful → Helpful used to
/// pay twice. The award is keyed on voter and guide: one voter's helpful vote on one guide pays
/// once, however often it is cast.
/// </para>
/// </summary>
public sealed class GuideVoteReputationConsumer
    : IntegrationEventHandler<GuideVoteCastIntegrationEvent>
{
    private const string Helpful = "Helpful";

    private readonly ReputationService _reputation;
    private readonly TrustProfileProvisioner _provisioner;
    private readonly IUnitOfWork _unitOfWork;

    public GuideVoteReputationConsumer(
        ReputationService reputation,
        TrustProfileProvisioner provisioner,
        IUnitOfWork unitOfWork)
    {
        _reputation = reputation;
        _provisioner = provisioner;
        _unitOfWork = unitOfWork;
    }

    protected override async Task HandleAsync(
        GuideVoteCastIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.AuthorUserId is not { } author
            || !string.Equals(integrationEvent.Kind, Helpful, StringComparison.OrdinalIgnoreCase)
            || author == integrationEvent.VoterUserId)
        {
            return;
        }

        var authorId = UserId.From(author);

        await _provisioner.GetOrCreateBothAsync(authorId, cancellationToken);

        await _reputation.ScoreAsync(
            authorId,
            ReputationActions.HelpfulVoteReceived,
            ReputationAwardKey.HelpfulVote(integrationEvent.GuideId, integrationEvent.VoterUserId),
            "guide",
            integrationEvent.GuideId.ToString(),
            cancellationToken: cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Pays an author when they publish a community update.</summary>
public sealed class CommunityPostReputationConsumer
    : IntegrationEventHandler<CommunityPostPublishedIntegrationEvent>
{
    private readonly ReputationService _reputation;
    private readonly TrustProfileProvisioner _provisioner;
    private readonly IUnitOfWork _unitOfWork;

    public CommunityPostReputationConsumer(
        ReputationService reputation,
        TrustProfileProvisioner provisioner,
        IUnitOfWork unitOfWork)
    {
        _reputation = reputation;
        _provisioner = provisioner;
        _unitOfWork = unitOfWork;
    }

    protected override async Task HandleAsync(
        CommunityPostPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var authorId = UserId.From(integrationEvent.AuthorId);

        await _provisioner.GetOrCreateBothAsync(authorId, cancellationToken);

        await _reputation.ScoreAsync(
            authorId,
            ReputationActions.UpdatePublished,
            ReputationAwardKey.CommunityPost(integrationEvent.PostId),
            "post",
            integrationEvent.PostId.ToString(),
            integrationEvent.CountryCode,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Pays a member for writing a comment.
/// <para>
/// Two points, capped at two hundred and fifty by the scoring table — enough that participating
/// counts for something and little enough that it is not worth farming.
/// </para>
/// </summary>
public sealed class CommunityCommentReputationConsumer
    : IntegrationEventHandler<CommunityCommentAddedIntegrationEvent>
{
    private readonly ReputationService _reputation;
    private readonly TrustProfileProvisioner _provisioner;
    private readonly IUnitOfWork _unitOfWork;

    public CommunityCommentReputationConsumer(
        ReputationService reputation,
        TrustProfileProvisioner provisioner,
        IUnitOfWork unitOfWork)
    {
        _reputation = reputation;
        _provisioner = provisioner;
        _unitOfWork = unitOfWork;
    }

    protected override async Task HandleAsync(
        CommunityCommentAddedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var authorId = UserId.From(integrationEvent.AuthorId);

        await _provisioner.GetOrCreateBothAsync(authorId, cancellationToken);

        await _reputation.ScoreAsync(
            authorId,
            ReputationActions.CommentWritten,
            ReputationAwardKey.Comment(integrationEvent.CommentId),
            "comment",
            integrationEvent.CommentId.ToString(),
            cancellationToken: cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Pays a member for adding a profile photo.
/// <para>
/// Once per member, ever: the rule allows one occurrence and the award key is the member. Removing
/// the photo publishes the same event with no media, and pays nothing; replacing it is not a second
/// first photo.
/// </para>
/// </summary>
public sealed class ProfilePhotoReputationConsumer
    : IntegrationEventHandler<UserAvatarChangedIntegrationEvent>
{
    private readonly ReputationService _reputation;
    private readonly TrustProfileProvisioner _provisioner;
    private readonly IUnitOfWork _unitOfWork;

    public ProfilePhotoReputationConsumer(
        ReputationService reputation,
        TrustProfileProvisioner provisioner,
        IUnitOfWork unitOfWork)
    {
        _reputation = reputation;
        _provisioner = provisioner;
        _unitOfWork = unitOfWork;
    }

    protected override async Task HandleAsync(
        UserAvatarChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.MediaId is not { } mediaId)
        {
            return;
        }

        var userId = UserId.From(integrationEvent.UserId);

        await _provisioner.GetOrCreateBothAsync(userId, cancellationToken);

        await _reputation.ScoreAsync(
            userId,
            ReputationActions.ProfilePhotoAdded,
            ReputationAwardKey.ProfilePhoto(integrationEvent.UserId),
            "media",
            mediaId.ToString(),
            cancellationToken: cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Pays an author when the community verifies their post.
/// <para>
/// <b>Not a second payment for the confirmations.</b> Each confirmation already pays the author
/// <c>CommunityConfirmation</c>, once per confirmer (<c>CommunityCorroborationConsumer</c>). This
/// is the post crossing the threshold, once per post: a different fact, priced by its own rule.
/// </para>
/// <para>
/// The event fires on every change of verification state, including losing it. Only a move into
/// community-verified pays, and the award key, one per post, means a post that is disputed and
/// verified again does not pay twice. An event that does not name the author was published before
/// the field existed and pays nothing.
/// </para>
/// </summary>
public sealed class PostVerifiedReputationConsumer
    : IntegrationEventHandler<CommunityPostVerificationChangedIntegrationEvent>
{
    /// <summary>Community's wire name for the state, as the contract documents it.</summary>
    private const string CommunityVerified = "communityVerified";

    private readonly ReputationService _reputation;
    private readonly TrustProfileProvisioner _provisioner;
    private readonly IUnitOfWork _unitOfWork;

    public PostVerifiedReputationConsumer(
        ReputationService reputation,
        TrustProfileProvisioner provisioner,
        IUnitOfWork unitOfWork)
    {
        _reputation = reputation;
        _provisioner = provisioner;
        _unitOfWork = unitOfWork;
    }

    protected override async Task HandleAsync(
        CommunityPostVerificationChangedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (!string.Equals(integrationEvent.Current, CommunityVerified, StringComparison.OrdinalIgnoreCase)
            || integrationEvent.AuthorUserId is not { } author)
        {
            return;
        }

        var authorId = UserId.From(author);

        await _provisioner.GetOrCreateBothAsync(authorId, cancellationToken);

        await _reputation.ScoreAsync(
            authorId,
            ReputationActions.UpdateVerified,
            ReputationAwardKey.UpdateVerified(integrationEvent.PostId),
            "post",
            integrationEvent.PostId.ToString(),
            integrationEvent.CountryCode,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Pays the referrer when somebody they invited qualifies: joined and made a first contribution
/// (RWD-03). 25 XP and, through the rule's credit points, 250 reward points (D-12, Q2).
/// <para>
/// Creator decides qualification and keeps the anti-abuse checks; Trust owns what it is worth. The
/// award key is the referral, so a redelivered qualification pays once.
/// </para>
/// </summary>
public sealed class ReferralQualifiedReputationConsumer
    : IntegrationEventHandler<Diaspora.Contracts.Creator.V1.Events.CreatorReferralQualifiedIntegrationEvent>
{
    private readonly ReputationService _reputation;
    private readonly TrustProfileProvisioner _provisioner;
    private readonly IUnitOfWork _unitOfWork;

    public ReferralQualifiedReputationConsumer(
        ReputationService reputation,
        TrustProfileProvisioner provisioner,
        IUnitOfWork unitOfWork)
    {
        _reputation = reputation;
        _provisioner = provisioner;
        _unitOfWork = unitOfWork;
    }

    protected override async Task HandleAsync(
        Diaspora.Contracts.Creator.V1.Events.CreatorReferralQualifiedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var referrer = UserId.From(integrationEvent.ReferrerUserId);

        await _provisioner.GetOrCreateBothAsync(referrer, cancellationToken);

        await _reputation.ScoreAsync(
            referrer,
            ReputationActions.ReferralQualified,
            ReputationAwardKey.Referral(integrationEvent.ReferralId),
            "referral",
            integrationEvent.ReferralId.ToString(),
            cancellationToken: cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Pays a member 10 XP for finishing an Academy course, once per course (RWD-12, Q6). No points: a
/// course stopped paying them for new completions.
/// </summary>
public sealed class CourseCompletedReputationConsumer
    : IntegrationEventHandler<Diaspora.Contracts.Creator.V1.Events.CourseCompletedIntegrationEvent>
{
    private readonly ReputationService _reputation;
    private readonly TrustProfileProvisioner _provisioner;
    private readonly IUnitOfWork _unitOfWork;

    public CourseCompletedReputationConsumer(
        ReputationService reputation,
        TrustProfileProvisioner provisioner,
        IUnitOfWork unitOfWork)
    {
        _reputation = reputation;
        _provisioner = provisioner;
        _unitOfWork = unitOfWork;
    }

    protected override async Task HandleAsync(
        Diaspora.Contracts.Creator.V1.Events.CourseCompletedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var member = UserId.From(integrationEvent.UserId);

        await _provisioner.GetOrCreateBothAsync(member, cancellationToken);

        await _reputation.ScoreAsync(
            member,
            ReputationActions.CourseCompleted,
            ReputationAwardKey.Course(integrationEvent.CourseId),
            "course",
            integrationEvent.CourseId.ToString(),
            cancellationToken: cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
