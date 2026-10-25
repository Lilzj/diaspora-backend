using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Contracts.Guides.V1.Events;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Domain;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Trust.Application.Consumers;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// WHAT IS PAID FOR CONTENT COMES BACK WITH THE CONTENT (D-9, REP-12)
//
// `ReputationAwardConsumers` pays for a guide, a post, a comment, a confirmation and a verification.
// Nothing took any of it back. A guide published and deleted the same minute kept its 50 points, a
// comment removed by a moderator kept its 2, and a revoked verification kept its award. A fine
// refunded on appeal stayed taken.
//
// These consumers are the other half. Each listens to the event that ends what an award was for
// and reverses it through `ReputationService`, which appends a compensating event that names the
// original (`ReversesEventId`) rather than deleting anything: the history stays append-only, and
// the caps count net of reversals, so a removed comment gives its place under the daily cap back.
//
// WHY BY SOURCE, NOT BY MEMBER
//
// Every award records what earned it (`SourceType`, `SourceId`). The removal events do not always
// name whose content it was: `CommunityCommentRemoved` carries no author. Finding the awards by
// source, across profiles, finds the comment's award and every confirmation on it without one.
// The source type is left open because a confirmation records the content type the reaction named
// ("post", "guide"), not the type the author's own award used; the ids are unique either way.
//
// WHAT IS NOT REVERSED
//
//   * `VerificationExpired`. A permit running out is not a finding against the member, and the
//     trust tier already reacts to the lapse.
//   * An author archiving their own guide (Q4). The knowledge is dated, not wrong.
//   * An expired post. Expiry is the post's lifespan, not a removal.
//   * Money. Points that have vested and credit already converted are never taken back
//     automatically (D-9); the points half of a reversal is CS-B3, and stops at vesting.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Takes back everything a guide earned when it is deleted, by its author or anyone else.</summary>
public sealed class GuideDeletedReversalConsumer
    : IntegrationEventHandler<GuideDeletedIntegrationEvent>
{
    public const string Reason = "Guide deleted.";

    private readonly ReputationService _reputation;
    private readonly IUnitOfWork _unitOfWork;

    public GuideDeletedReversalConsumer(ReputationService reputation, IUnitOfWork unitOfWork)
    {
        _reputation = reputation;
        _unitOfWork = unitOfWork;
    }

    protected override async Task HandleAsync(
        GuideDeletedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // Published, approved, every paid update and every helpful vote: all sourced from the guide.
        var reversed = await _reputation.ReverseBySourceAsync(
            null, integrationEvent.GuideId.ToString(), Reason, ReversalScope.Awards,
            cancellationToken: cancellationToken);

        if (reversed > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}

/// <summary>
/// Takes back what a guide earned when someone other than its author archives it.
/// <para>
/// An author retiring their own guide keeps what it earned (Q4). An archive that names nobody is
/// the moderation projection's, which is a removal.
/// </para>
/// </summary>
public sealed class GuideArchivedReversalConsumer
    : IntegrationEventHandler<GuideArchivedIntegrationEvent>
{
    public const string Reason = "Guide archived.";

    private readonly ReputationService _reputation;
    private readonly IUnitOfWork _unitOfWork;

    public GuideArchivedReversalConsumer(ReputationService reputation, IUnitOfWork unitOfWork)
    {
        _reputation = reputation;
        _unitOfWork = unitOfWork;
    }

    protected override async Task HandleAsync(
        GuideArchivedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.ArchivedBy is { } archivedBy
            && archivedBy == integrationEvent.AuthorUserId)
        {
            return;
        }

        var reversed = await _reputation.ReverseBySourceAsync(
            null, integrationEvent.GuideId.ToString(), Reason, ReversalScope.Awards,
            cancellationToken: cancellationToken);

        if (reversed > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}

/// <summary>
/// Takes back what a post earned when it is deleted or removed: the post itself, its community
/// verification and every confirmation on it. An expired post keeps them.
/// </summary>
public sealed class CommunityPostRemovedReversalConsumer
    : IntegrationEventHandler<CommunityPostRemovedIntegrationEvent>
{
    public const string Reason = "Post removed.";

    private const string Expired = "expired";

    private readonly ReputationService _reputation;
    private readonly IUnitOfWork _unitOfWork;

    public CommunityPostRemovedReversalConsumer(ReputationService reputation, IUnitOfWork unitOfWork)
    {
        _reputation = reputation;
        _unitOfWork = unitOfWork;
    }

    protected override async Task HandleAsync(
        CommunityPostRemovedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (string.Equals(integrationEvent.Reason, Expired, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var reversed = await _reputation.ReverseBySourceAsync(
            null, integrationEvent.PostId.ToString(), Reason, ReversalScope.Awards,
            cancellationToken: cancellationToken);

        if (reversed > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}

/// <summary>
/// Takes back a removed comment's award. The event names no author; the award is found by the
/// comment it was for.
/// </summary>
public sealed class CommunityCommentRemovedReversalConsumer
    : IntegrationEventHandler<CommunityCommentRemovedIntegrationEvent>
{
    public const string Reason = "Comment removed.";

    private readonly ReputationService _reputation;
    private readonly IUnitOfWork _unitOfWork;

    public CommunityCommentRemovedReversalConsumer(ReputationService reputation, IUnitOfWork unitOfWork)
    {
        _reputation = reputation;
        _unitOfWork = unitOfWork;
    }

    protected override async Task HandleAsync(
        CommunityCommentRemovedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var reversed = await _reputation.ReverseBySourceAsync(
            null, integrationEvent.CommentId.ToString(), Reason, ReversalScope.Awards,
            cancellationToken: cancellationToken);

        if (reversed > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}

/// <summary>Takes back a revoked verification's approval award. An expiry reverses nothing.</summary>
public sealed class VerificationRevokedReversalConsumer
    : IntegrationEventHandler<VerificationRevokedIntegrationEvent>
{
    public const string Reason = "Verification revoked.";

    private readonly ReputationService _reputation;
    private readonly IUnitOfWork _unitOfWork;

    public VerificationRevokedReversalConsumer(ReputationService reputation, IUnitOfWork unitOfWork)
    {
        _reputation = reputation;
        _unitOfWork = unitOfWork;
    }

    protected override async Task HandleAsync(
        VerificationRevokedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // Sourced as VerificationGrantApplier records it.
        var reversed = await _reputation.ReverseBySourceAsync(
            "verification", integrationEvent.VerificationId.ToString(), Reason, ReversalScope.Awards,
            UserId.From(integrationEvent.UserId), cancellationToken);

        if (reversed > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}

/// <summary>
/// Refunds the fine an upheld appeal overturned: a content removal or a suspension.
/// <para>
/// <b>Matched on the content, then on the decision.</b> An appeal's target is free text. The
/// penalty records the content as its source and the moderation item in its award key, so a
/// target that names the content matches the first and one that names the moderation item
/// matches the second. Only the appellant's own penalties are refunded either way.
/// </para>
/// </summary>
public sealed class AppealApprovedRefundConsumer
    : IntegrationEventHandler<AppealApprovedIntegrationEvent>
{
    public const string Reason = "Appeal upheld.";

    private readonly ReputationService _reputation;
    private readonly IUnitOfWork _unitOfWork;

    public AppealApprovedRefundConsumer(ReputationService reputation, IUnitOfWork unitOfWork)
    {
        _reputation = reputation;
        _unitOfWork = unitOfWork;
    }

    protected override async Task HandleAsync(
        AppealApprovedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // A verification appeal grants what was refused, which pays its own award. A publishing
        // appeal restores a right, not a score.
        if (!Enum.TryParse<AppealType>(integrationEvent.AppealType, ignoreCase: true, out var type)
            || type is not (AppealType.Content or AppealType.Suspension)
            || string.IsNullOrWhiteSpace(integrationEvent.TargetId))
        {
            return;
        }

        var appellant = UserId.From(integrationEvent.UserId);
        var target = integrationEvent.TargetId.Trim();

        var refunded = await _reputation.ReverseBySourceAsync(
            null, target, Reason, ReversalScope.Penalties, appellant, cancellationToken);

        if (refunded == 0 && Guid.TryParse(target, out var moderationItemId))
        {
            refunded = await _reputation.ReverseAwardsAsync(
                appellant,
                [
                    ReputationAwardKey.Moderation(ReputationActions.ViolationRecorded, moderationItemId),
                    ReputationAwardKey.Moderation(ReputationActions.ContentRemoved, moderationItemId)
                ],
                Reason,
                cancellationToken);
        }

        if (refunded > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}

/// <summary>Which events a reversal by source undoes.</summary>
public static class ReversalScope
{
    /// <summary>What the content earned. Not the fine for it: a removal takes back the one and keeps the other.</summary>
    public static readonly Func<ReputationEvent, bool> Awards = item => item.Points > 0;

    /// <summary>What was taken for it. Only an upheld appeal gives these back.</summary>
    public static readonly Func<ReputationEvent, bool> Penalties = item => item.Points < 0;
}
