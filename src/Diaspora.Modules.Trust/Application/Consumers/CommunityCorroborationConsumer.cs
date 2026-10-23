using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Trust.Application.Consumers;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// THE JOIN BETWEEN THE TWO CONFIRMATION SYSTEMS
//
// The platform had two, and neither knew about the other:
//
//   * **B6 corroboration.** A `confirmed` reaction moves `CommunityPost.ConfirmationCount`,
//     re-evaluates the urgency-scaled threshold in `ConfirmationThresholds`, and flips the post
//     to `CommunityVerified` or `Disputed`. It is what every card's confirmation figures are
//     read from. It awarded the author **nothing**.
//   * **This module's confirmation.** `POST /trust/contributions/confirmations` awards the author
//     five trust points and records a `CommunityContribution`. It moved **no count**, and until
//     `GET /trust/contributions/confirmations` shipped alongside this consumer, nothing could
//     read what it wrote — `CountForTargetAsync` existed and had no callers.
//
// The client straddled the two: its alert card posted a Trust confirmation and rendered B6's
// count, so the number never moved and an alert could not reach `CommunityVerified` through the
// button offered for exactly that purpose.
//
// This consumer is the join, and it is deliberately one-directional. B6 keeps owning the count
// and the threshold — the numbers a reader sees are still computed in one place by one policy,
// which is what stops this becoming a third scoring system. Trust learns that a corroboration
// happened and does the one thing it owns: it pays the author.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Awards the author when the community corroborates their post.
/// <para>
/// <b>The <c>CommunityContribution</c> row is written for its unique index, not for its own
/// sake.</b> <see cref="ReputationService.ScoreAsync"/> deduplicates on nothing —
/// <c>CommunityConfirmation</c> carries no <c>MaxOccurrences</c> and no cooldown, so it pays out
/// every time it is called. Delivery here is at-least-once, and a member can withdraw a reaction
/// and re-add it all afternoon. The one-confirmation-per-member-per-target index is the only
/// thing standing between that and a points faucet, so the award goes through the same record
/// that guards the HTTP path.
/// </para>
/// </summary>
public sealed class CommunityCorroborationConsumer
    : IntegrationEventHandler<CommunityReactionAddedIntegrationEvent>
{
    /// <summary>B6's wire name for a corroborating reaction's sentiment.</summary>
    private const string CorroboratingSentiment = "corroborating";

    private readonly ICommunityContributionRepository _contributions;
    private readonly ReputationService _reputation;
    private readonly TrustProfileProvisioner _provisioner;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CommunityCorroborationConsumer(
        ICommunityContributionRepository contributions,
        ReputationService reputation,
        TrustProfileProvisioner provisioner,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _contributions = contributions;
        _reputation = reputation;
        _provisioner = provisioner;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    protected override async Task HandleAsync(
        CommunityReactionAddedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // Sentiment, not reaction key. The reaction catalogue is data — a deployment that adds a
        // second corroborating reaction should pay the author for it without a release here,
        // which is the whole reason B6 publishes the sentiment as well as the key.
        if (!string.Equals(
                integrationEvent.Sentiment, CorroboratingSentiment, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Content another module owns travels on this contract with no author attached. There is
        // nobody to pay, and guessing one from the reactor would pay the wrong member.
        if (integrationEvent.ContentAuthorId is not { } authorGuid)
        {
            return;
        }

        var authorId = UserId.From(authorGuid);
        var confirmerId = UserId.From(integrationEvent.UserId);

        // B6 refuses self-corroboration at the point of reaction, so this should not arrive.
        // Checked anyway: this handler pays points, and a rule that silently depends on another
        // module keeping its guard is a rule that stops holding the day that guard moves.
        if (authorId == confirmerId)
        {
            return;
        }

        // The event's own content type is the target type, verbatim. Naming it anything else
        // would leave the tally split across two spellings of the same thing.
        var targetType = integrationEvent.ContentType;
        var targetId = integrationEvent.ContentId.ToString();

        var already = await _contributions
            .ExistsAsync(confirmerId, targetType, targetId, cancellationToken)
            .ConfigureAwait(false);

        if (already)
        {
            // A redelivery, or a member who withdrew and re-reacted. Both are correct outcomes:
            // the count in B6 moves with the reaction, the author is paid once.
            return;
        }

        CommunityContribution confirmation;
        try
        {
            confirmation = CommunityContribution.Record(
                confirmerId,
                authorId,
                targetType,
                targetId,
                _clock.UtcNow,
                integrationEvent.CountryCode);
        }
        catch (DomainException)
        {
            // The aggregate refuses self-confirmation too. Nothing to retry — swallowing here
            // rather than throwing keeps a malformed event out of the dead-letter queue.
            return;
        }

        await _contributions.AddAsync(confirmation, cancellationToken).ConfigureAwait(false);

        // A member can be corroborated before they have ever opened the trust centre.
        await _provisioner.GetOrCreateBothAsync(authorId, cancellationToken).ConfigureAwait(false);

        await _reputation.ScoreAsync(
            authorId,
            ReputationActions.CommunityConfirmation,
            ReputationAwardKey.Confirmation(targetType, targetId, confirmerId.Value),
            targetType,
            targetId,
            integrationEvent.CountryCode,
            cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
