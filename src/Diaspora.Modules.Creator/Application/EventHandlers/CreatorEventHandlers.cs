using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Creator.V1.Events;
using Diaspora.Contracts.Events;
using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Domain.Events;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Platform.Messaging;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Creator.Application.EventHandlers;

/// <summary>
/// Turns the Creator Platform's domain events into the published contracts other modules consume.
/// <para>
/// <b>The one translation seam, and the reason this module's domain stays private.</b> A domain event
/// carries strongly typed identifiers and domain enums; an integration event carries flat primitives,
/// so a consumer can parse it without referencing this module. Doing the translation in a single
/// class means the two never drift.
/// </para>
/// <para>
/// <b>A publish failure never fails the write.</b> The aggregate is already saved by the time a
/// domain event handler runs; throwing here would roll back a completed decision because a
/// notification did not go out. Failures are logged, and at-least-once delivery is the contract —
/// consumers de-duplicate on <c>EventId</c>.
/// </para>
/// <para>
/// <b>Everything goes through <see cref="IIntegrationEventPublisher"/>.</b> Its only implementation
/// writes an outbox row inside the caller's transaction, which is what makes "the fact was recorded
/// and the fact was announced" one atomic decision — DSP2002.
/// </para>
/// </summary>
public sealed class CreatorIntegrationEventRelay
{
    private readonly IIntegrationEventPublisher _publisher;
    private readonly ILogger<CreatorIntegrationEventRelay> _logger;

    public CreatorIntegrationEventRelay(
        IIntegrationEventPublisher publisher, ILogger<CreatorIntegrationEventRelay> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public Task PublishAsync(
        CreatorStatusChangedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new CreatorStatusChangedIntegrationEvent
            {
                CreatorId = domainEvent.CreatorId.Value,
                MemberUserId = domainEvent.UserId,
                PreviousStatus = domainEvent.PreviousStatus.ToString(),
                CurrentStatus = domainEvent.CurrentStatus.ToString(),
                PolicyVersion = CreatorStatusPolicy.Version,
                ChangedAt = domainEvent.ChangedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        CreatorVerificationDecidedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new CreatorVerificationDecidedIntegrationEvent
            {
                ApplicationId = domainEvent.ApplicationId.Value,
                CreatorId = domainEvent.CreatorId.Value,
                MemberUserId = domainEvent.UserId,
                PreviousState = domainEvent.PreviousState.ToString(),
                CurrentState = domainEvent.CurrentState.ToString(),
                Reason = domainEvent.Reason,
                DecidedAt = domainEvent.DecidedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        CreatorMilestoneCompletedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new CreatorMilestoneCompletedIntegrationEvent
            {
                MilestoneId = domainEvent.MilestoneId.Value,
                CreatorId = domainEvent.CreatorId.Value,
                MemberUserId = domainEvent.UserId,
                MilestoneKey = domainEvent.MilestoneKey,
                CompletedAt = domainEvent.CompletedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        ChallengeProgressChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new CreatorChallengeProgressChangedIntegrationEvent
            {
                ChallengeId = domainEvent.ChallengeId.Value,
                CreatorId = domainEvent.CreatorId.Value,
                MemberUserId = domainEvent.UserId,
                Current = domainEvent.Current,
                Target = domainEvent.Target,
                Percentage = domainEvent.Percentage,
                ChangedAt = domainEvent.ChangedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        ChallengeCompletedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new CreatorChallengeCompletedIntegrationEvent
            {
                ChallengeId = domainEvent.ChallengeId.Value,
                CreatorId = domainEvent.CreatorId.Value,
                MemberUserId = domainEvent.UserId,
                CompletedAt = domainEvent.CompletedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        GrantApplicationDecidedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new GrantApplicationDecidedIntegrationEvent
            {
                ApplicationId = domainEvent.ApplicationId.Value,
                GrantId = domainEvent.GrantId.Value,
                CreatorId = domainEvent.CreatorId.Value,
                ApplicantUserId = domainEvent.ApplicantUserId,
                PreviousState = domainEvent.PreviousState.ToString(),
                CurrentState = domainEvent.CurrentState.ToString(),
                AwardedMinorUnits = domainEvent.AwardedMinorUnits,
                Currency = domainEvent.Currency,
                Reason = domainEvent.Reason,
                DecidedAt = domainEvent.DecidedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        ReferralQualifiedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new CreatorReferralQualifiedIntegrationEvent
            {
                ReferralId = domainEvent.ReferralId.Value,
                ReferrerCreatorId = domainEvent.ReferrerCreatorId.Value,
                ReferrerUserId = domainEvent.ReferrerUserId,
                InviteeUserId = domainEvent.InviteeUserId,
                RewardPoints = domainEvent.RewardPoints,
                QualificationVersion = ReferralQualificationPolicy.Version,
                QualifiedAt = domainEvent.QualifiedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        SignupBonusAwardedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new SignupBonusAwardedIntegrationEvent
            {
                MemberUserId = domainEvent.UserId,
                Points = domainEvent.Points,
                AwardedAt = domainEvent.AwardedAt
            },
            cancellationToken);
    }

    /// <summary>
    /// The one place this module awards points, and it awards none itself.
    /// <para>
    /// <b>B15's own contract, which B15 already consumes.</b> The Financial Platform holds the single
    /// points balance; this module publishes the fact that points were earned and never touches a
    /// figure. <c>GrantReference</c> is the anti-double-award barrier and is uniquely indexed on B15's
    /// account, so a redelivered message awards once.
    /// </para>
    /// </summary>
    public Task PublishAsync(
        CreatorPointsEarnedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new RewardPointsGrantedIntegrationEvent
            {
                MemberUserId = domainEvent.UserId,
                Points = domainEvent.Points,
                Source = domainEvent.Source,
                GrantReference = domainEvent.GrantReference,
                GrantedAt = domainEvent.EarnedAt
            },
            cancellationToken);
    }

    private async Task PublishSafelyAsync<TEvent>(
        TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        try
        {
            await _publisher.PublishAsync(integrationEvent, cancellationToken);
        }
#pragma warning disable CA1031 // A publish failure must not roll back a decision that already landed.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            _logger.LogError(
                exception,
                "Failed to publish {EventType} from the Creator Platform.",
                typeof(TEvent).Name);
        }
    }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════
// Domain event handlers.
//
// One settling handler per event — DSP1002 reports a second — and each does exactly one thing:
// translate and publish. None of them writes, because the aggregate that raised the event has
// already been saved by the time these run.
// ════════════════════════════════════════════════════════════════════════════════════════════════

public sealed class CreatorStatusChangedHandler
    : DomainEventHandler<CreatorStatusChangedDomainEvent>
{
    private readonly CreatorIntegrationEventRelay _relay;

    public CreatorStatusChangedHandler(CreatorIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        CreatorStatusChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}

public sealed class CreatorVerificationDecidedHandler
    : DomainEventHandler<CreatorVerificationDecidedDomainEvent>
{
    private readonly CreatorIntegrationEventRelay _relay;

    public CreatorVerificationDecidedHandler(CreatorIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        CreatorVerificationDecidedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}

public sealed class CreatorMilestoneCompletedHandler
    : DomainEventHandler<CreatorMilestoneCompletedDomainEvent>
{
    private readonly CreatorIntegrationEventRelay _relay;

    public CreatorMilestoneCompletedHandler(CreatorIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        CreatorMilestoneCompletedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}

public sealed class ChallengeProgressChangedHandler
    : DomainEventHandler<ChallengeProgressChangedDomainEvent>
{
    private readonly CreatorIntegrationEventRelay _relay;

    public ChallengeProgressChangedHandler(CreatorIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        ChallengeProgressChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}

public sealed class ChallengeCompletedHandler : DomainEventHandler<ChallengeCompletedDomainEvent>
{
    private readonly CreatorIntegrationEventRelay _relay;

    public ChallengeCompletedHandler(CreatorIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        ChallengeCompletedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}

public sealed class GrantApplicationDecidedHandler
    : DomainEventHandler<GrantApplicationDecidedDomainEvent>
{
    private readonly CreatorIntegrationEventRelay _relay;

    public GrantApplicationDecidedHandler(CreatorIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        GrantApplicationDecidedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}

public sealed class ReferralQualifiedHandler : DomainEventHandler<ReferralQualifiedDomainEvent>
{
    private readonly CreatorIntegrationEventRelay _relay;

    public ReferralQualifiedHandler(CreatorIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        ReferralQualifiedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}

public sealed class SignupBonusAwardedHandler : DomainEventHandler<SignupBonusAwardedDomainEvent>
{
    private readonly CreatorIntegrationEventRelay _relay;

    public SignupBonusAwardedHandler(CreatorIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        SignupBonusAwardedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}

/// <summary>
/// Publishes points earned into B15's own contract.
/// <para>
/// Separate from the eight above because it publishes a <em>Financial</em> contract rather than a
/// creator one — the module's own events describe what happened to a creator, and this one asks the
/// Financial Platform to record what that is worth.
/// </para>
/// </summary>
public sealed class CreatorPointsEarnedHandler : DomainEventHandler<CreatorPointsEarnedDomainEvent>
{
    private readonly CreatorIntegrationEventRelay _relay;

    public CreatorPointsEarnedHandler(CreatorIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        CreatorPointsEarnedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
