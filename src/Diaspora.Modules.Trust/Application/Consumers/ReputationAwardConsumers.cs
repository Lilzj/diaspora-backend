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
//   * `ProfileCompleted`, `ProfilePhotoAdded` — Identity publishes three integration events and
//     none of them is a profile change.
//   * `UpdateVerified` — `CommunityPostVerificationChangedIntegrationEvent` carries the post and
//     its confirmation counts, and not the author. Trust cannot look that up; it is Community's
//     data. Paying it needs one field added to that contract.
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
// `ScoreAsync` does not deduplicate on source. Two producers for one action means two payments,
// so before adding a consumer here, check `VerificationTypeHandlers.cs` for the same action.
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

        var authorId = UserId.From(integrationEvent.AuthorId);

        await _provisioner.GetOrCreateBothAsync(authorId, cancellationToken);

        await _reputation.ScoreAsync(
            authorId,
            ReputationActions.GuidePublished,
            "guide",
            integrationEvent.GuideId.ToString(),
            integrationEvent.CountryCode,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Pays an author for a substantive update to their guide.
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

        if (integrationEvent.AuthorUserId is not { } author)
        {
            return;
        }

        var authorId = UserId.From(author);

        await _provisioner.GetOrCreateBothAsync(authorId, cancellationToken);

        await _reputation.ScoreAsync(
            authorId,
            ReputationActions.GuideUpdated,
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
/// a verified state pays, and the rule's single-occurrence cap means a guide that flickers does
/// not pay twice.
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
            "comment",
            integrationEvent.CommentId.ToString(),
            cancellationToken: cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}


