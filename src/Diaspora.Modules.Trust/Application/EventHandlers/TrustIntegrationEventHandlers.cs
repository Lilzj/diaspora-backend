using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Domain.Events;
using Diaspora.Modules.Trust.Observability;
using Diaspora.Platform.Messaging;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Trust.Application.EventHandlers;

/// <summary>
/// Translates the module's domain events into published integration events, and — where
/// ADR-002 requires it — into a trust decision sent to Identity.
/// <para>
/// The translation layer is the point. A domain event carries the module's own types and is
/// free to change; an integration event is a frozen contract other services parse. Keeping
/// them separate means a refactor inside Trust cannot silently break a consumer, and it is
/// where the future outbox will sit: every outbound message already passes through exactly
/// these handlers.
/// </para>
/// <para>
/// <b>B1.2 changed how these publish, and nothing else.</b> Every handler below used to hand its
/// integration event to <c>IDomainEventDispatcher</c> — legal only because <c>IntegrationEvent</c>
/// derived from <c>DomainEvent</c>, and the reason B1.1 could not simply intercept the publisher
/// port. Trust alone accounted for most of the platform's published contracts taking that route.
/// The events, their payloads and their ordering are unchanged; they now travel through
/// <see cref="IIntegrationEventPublisher"/>, which is the same outbox row by a route the compiler
/// can check.
/// </para>
/// <para>
/// <b>On failure.</b> These run after the aggregate is saved, and publishing no longer means
/// running consumers — it means writing a row into the transaction that is already open. It
/// therefore fails only if the event cannot be serialized or is oversized, both defects in the
/// event itself, and both better as a rolled-back decision than as a decision committed with its
/// announcement lost. The B1.1 gap this comment used to name — a publish failure silently
/// dropping a verification decision — was closed by the outbox and is not reintroduced here.
/// </para>
/// </summary>
public sealed class VerificationSubmittedEventHandler
    : DomainEventHandler<VerificationSubmittedDomainEvent>
{
    private readonly IIntegrationEventPublisher _events;
    private readonly ITrustDecisionPublisher _decisions;
    private readonly ILogger<VerificationSubmittedEventHandler> _logger;

    public VerificationSubmittedEventHandler(
        IIntegrationEventPublisher events,
        ITrustDecisionPublisher decisions,
        ILogger<VerificationSubmittedEventHandler> logger)
    {
        _events = events;
        _decisions = decisions;
        _logger = logger;
    }

    protected override async Task HandleAsync(
        VerificationSubmittedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await _events.PublishAsync(
            new VerificationSubmittedIntegrationEvent
            {
                VerificationId = domainEvent.RequestId.Value,
                UserId = domainEvent.UserId.Value,
                VerificationType = domainEvent.Type.ToString(),
                Nationality = domainEvent.Nationality,
                ResidenceCountry = domainEvent.ResidenceCountry,
                TargetCountry = domainEvent.TargetCountry,
                SubmittedAt = domainEvent.At
            },
            cancellationToken);

        // Only the types that gate community access move the member's Identity state. A
        // professional credential going to review should not put someone's whole account
        // into PendingVerification.
        if (domainEvent.Type is VerificationType.Diaspora or VerificationType.Identity
            or VerificationType.Country)
        {
            // Submitted, not UnderReview: nobody has picked it up yet, the request row says
            // Pending, and VerificationUnderReview is not reachable from LimitedAccess anyway.
            // Publishing the wrong rung is how this moved nobody for as long as it existed.
            await _decisions.PublishAsync(
                domainEvent.UserId,
                TrustDecisionOutcome.Submitted,
                $"{domainEvent.Type} verification submitted.",
                "trust-engine",
                cancellationToken);
        }

        _logger.LogInformation(
            "Verification {VerificationId} ({Type}) submitted by {UserId}.",
            domainEvent.RequestId.Value, domainEvent.Type, domainEvent.UserId.Value);
    }
}

/// <summary>
/// Publishes an approval, tells Identity to grant, and raises the derived
/// contributor/guide/organisation events other modules subscribe to.
/// </summary>
public sealed class VerificationApprovedEventHandler
    : DomainEventHandler<VerificationApprovedDomainEvent>
{
    private readonly IIntegrationEventPublisher _events;
    private readonly ITrustDecisionPublisher _decisions;

    public VerificationApprovedEventHandler(
        IIntegrationEventPublisher events, ITrustDecisionPublisher decisions)
    {
        _events = events;
        _decisions = decisions;
    }

    protected override async Task HandleAsync(
        VerificationApprovedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await _events.PublishAsync(
            new VerificationApprovedIntegrationEvent
            {
                VerificationId = domainEvent.RequestId.Value,
                UserId = domainEvent.UserId.Value,
                VerificationType = domainEvent.Type.ToString(),
                TargetCountry = domainEvent.TargetCountry,
                Partial = domainEvent.Partial,
                ExpiresAt = domainEvent.ExpiresAt,
                ApprovedAt = domainEvent.At
            },
            cancellationToken);

        // The narrower events, so a consumer interested only in "someone became a guide" does
        // not have to inspect every approval's type.
        switch (domainEvent.Type)
        {
            case VerificationType.Contributor:
                await _events.PublishAsync(
                    new ContributorApprovedIntegrationEvent
                    {
                        UserId = domainEvent.UserId.Value,
                        CountryCode = domainEvent.TargetCountry,
                        ApprovedAt = domainEvent.At
                    },
                    cancellationToken);
                break;

            case VerificationType.CommunityGuide:
                await _events.PublishAsync(
                    new GuideApprovedIntegrationEvent
                    {
                        UserId = domainEvent.UserId.Value,
                        CountryCode = domainEvent.TargetCountry,
                        ApprovedAt = domainEvent.At
                    },
                    cancellationToken);
                break;

            case VerificationType.Organization:
                await _events.PublishAsync(
                    new OrganizationVerifiedIntegrationEvent
                    {
                        UserId = domainEvent.UserId.Value,
                        VerificationId = domainEvent.RequestId.Value,

                        // B3.0.1. Its two siblings above already carried the country; this one
                        // did not, for no reason a reader could reconstruct.
                        CountryCode = domainEvent.TargetCountry,
                        VerifiedAt = domainEvent.At
                    },
                    cancellationToken);
                break;
        }

        if (domainEvent.Type is VerificationType.Diaspora or VerificationType.Identity
            or VerificationType.Country)
        {
            await _decisions.PublishAsync(
                domainEvent.UserId,
                TrustDecisionOutcome.Granted,
                $"{domainEvent.Type} verification approved.",
                "trust-engine",
                cancellationToken);
        }
    }
}

public sealed class VerificationRejectedEventHandler
    : DomainEventHandler<VerificationRejectedDomainEvent>
{
    private readonly IIntegrationEventPublisher _events;
    private readonly ITrustDecisionPublisher _decisions;

    public VerificationRejectedEventHandler(
        IIntegrationEventPublisher events, ITrustDecisionPublisher decisions)
    {
        _events = events;
        _decisions = decisions;
    }

    protected override async Task HandleAsync(
        VerificationRejectedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await _events.PublishAsync(
            new VerificationRejectedIntegrationEvent
            {
                VerificationId = domainEvent.RequestId.Value,
                UserId = domainEvent.UserId.Value,
                VerificationType = domainEvent.Type.ToString(),

                // B3.0.1. Null for every type but Country, because that is the only claim that
                // names a community — not because the value was unavailable.
                TargetCountry = domainEvent.TargetCountry,
                ReasonCode = domainEvent.Reason.ToString(),
                Details = domainEvent.Details,
                RejectedAt = domainEvent.At
            },
            cancellationToken);

        if (domainEvent.Type is VerificationType.Diaspora or VerificationType.Identity
            or VerificationType.Country)
        {
            await _decisions.PublishAsync(
                domainEvent.UserId,
                TrustDecisionOutcome.Rejected,
                domainEvent.Details ?? $"{domainEvent.Type} verification rejected.",
                "trust-engine",
                cancellationToken);
        }
    }
}

public sealed class VerificationExpiredEventHandler
    : DomainEventHandler<VerificationExpiredDomainEvent>
{
    private readonly IIntegrationEventPublisher _events;
    private readonly ITrustDecisionPublisher _decisions;

    public VerificationExpiredEventHandler(IIntegrationEventPublisher events, ITrustDecisionPublisher decisions)
    {
        _events = events;
        _decisions = decisions;
    }

    protected override async Task HandleAsync(
        VerificationExpiredDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await _events.PublishAsync(
            new VerificationExpiredIntegrationEvent
            {
                VerificationId = domainEvent.RequestId.Value,
                UserId = domainEvent.UserId.Value,
                VerificationType = domainEvent.Type.ToString(),

                // B3.0.1. A consumer could already match this lapse to a country by the
                // verification id it recorded at approval — but only if it saw the approval.
                TargetCountry = domainEvent.TargetCountry,
                ExpiredAt = domainEvent.At
            },
            cancellationToken);

        // A lapse revokes rather than rejects. The distinction matters to Identity: a member
        // whose permit expired has not been refused, and should be able to re-verify without
        // the account history of a rejection.
        if (domainEvent.Type is VerificationType.Diaspora or VerificationType.Country)
        {
            await _decisions.PublishAsync(
                domainEvent.UserId,
                TrustDecisionOutcome.Revoked,
                $"{domainEvent.Type} verification expired.",
                "trust-engine",
                cancellationToken);
        }
    }
}

public sealed class VerificationRevokedEventHandler
    : DomainEventHandler<VerificationRevokedDomainEvent>
{
    private readonly IIntegrationEventPublisher _events;
    private readonly ITrustDecisionPublisher _decisions;

    public VerificationRevokedEventHandler(IIntegrationEventPublisher events, ITrustDecisionPublisher decisions)
    {
        _events = events;
        _decisions = decisions;
    }

    protected override async Task HandleAsync(
        VerificationRevokedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await _events.PublishAsync(
            new VerificationRevokedIntegrationEvent
            {
                VerificationId = domainEvent.RequestId.Value,
                UserId = domainEvent.UserId.Value,
                VerificationType = domainEvent.Type.ToString(),

                // B3.0.1, for the same reason as the expiry above.
                TargetCountry = domainEvent.TargetCountry,
                Reason = domainEvent.Reason,
                RevokedAt = domainEvent.At
            },
            cancellationToken);

        await _decisions.PublishAsync(
            domainEvent.UserId,
            TrustDecisionOutcome.Revoked,
            domainEvent.Reason,
            "trust-engine",
            cancellationToken);
    }
}

public sealed class TrustLevelChangedEventHandler : DomainEventHandler<TrustLevelChangedDomainEvent>
{
    private readonly IIntegrationEventPublisher _events;

    public TrustLevelChangedEventHandler(IIntegrationEventPublisher events)
    {
        _events = events;
    }

    protected override async Task HandleAsync(
        TrustLevelChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await _events.PublishAsync(
            new TrustLevelChangedIntegrationEvent
            {
                UserId = domainEvent.UserId.Value,
                PreviousLevel = domainEvent.From.ToString(),
                CurrentLevel = domainEvent.To.ToString(),
                // Carried alongside so a consumer that only cares about the coarse ladder
                // does not need to know the module's finer one.
                PlatformTrustLevel = TrustEngineService.ToPlatformLevel(domainEvent.To).ToString(),
                Reason = domainEvent.Reason,
                ChangedAt = domainEvent.At
            },
            cancellationToken);

        TrustTelemetrySource.TrustLevelChanges.Add(
            1,
            new KeyValuePair<string, object?>(
                "direction", domainEvent.To > domainEvent.From ? "up" : "down"));
    }
}

public sealed class BadgeAwardedEventHandler : DomainEventHandler<BadgeAwardedDomainEvent>
{
    private readonly IIntegrationEventPublisher _events;

    public BadgeAwardedEventHandler(IIntegrationEventPublisher events)
    {
        _events = events;
    }

    protected override async Task HandleAsync(
        BadgeAwardedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await _events.PublishAsync(
            new BadgeAwardedIntegrationEvent
            {
                UserId = domainEvent.UserId.Value,
                Badge = domainEvent.Badge.ToString(),
                CountryCode = domainEvent.CountryCode,
                AwardedAt = domainEvent.At
            },
            cancellationToken);

        TrustTelemetrySource.BadgesAwarded.Add(
            1, new KeyValuePair<string, object?>("badge", domainEvent.Badge.ToString()));
    }
}

public sealed class BadgeRevokedEventHandler : DomainEventHandler<BadgeRevokedDomainEvent>
{
    private readonly IIntegrationEventPublisher _events;

    public BadgeRevokedEventHandler(IIntegrationEventPublisher events)
    {
        _events = events;
    }

    protected override Task HandleAsync(
        BadgeRevokedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return _events.PublishAsync(
            new BadgeRevokedIntegrationEvent
            {
                UserId = domainEvent.UserId.Value,
                Badge = domainEvent.Badge.ToString(),
                CountryCode = domainEvent.CountryCode,
                Reason = domainEvent.Reason,
                RevokedAt = domainEvent.At
            },
            cancellationToken);
    }
}

public sealed class ReputationLevelChangedEventHandler
    : DomainEventHandler<ReputationLevelChangedDomainEvent>
{
    private readonly IIntegrationEventPublisher _events;

    public ReputationLevelChangedEventHandler(IIntegrationEventPublisher events)
    {
        _events = events;
    }

    protected override Task HandleAsync(
        ReputationLevelChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return _events.PublishAsync(
            new ReputationLevelChangedIntegrationEvent
            {
                UserId = domainEvent.UserId.Value,
                PreviousLevel = domainEvent.From.ToString(),
                CurrentLevel = domainEvent.To.ToString(),
                CombinedScore = domainEvent.CombinedScore,
                ChangedAt = domainEvent.At
            },
            cancellationToken);
    }
}

public sealed class AppealSubmittedEventHandler : DomainEventHandler<AppealSubmittedDomainEvent>
{
    private readonly IIntegrationEventPublisher _events;

    public AppealSubmittedEventHandler(IIntegrationEventPublisher events)
    {
        _events = events;
    }

    protected override Task HandleAsync(
        AppealSubmittedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return _events.PublishAsync(
            new AppealSubmittedIntegrationEvent
            {
                AppealId = domainEvent.AppealId.Value,
                UserId = domainEvent.UserId.Value,
                AppealType = domainEvent.Type.ToString(),
                TargetId = domainEvent.TargetId,
                SubmittedAt = domainEvent.At
            },
            cancellationToken);
    }
}

public sealed class AppealApprovedEventHandler : DomainEventHandler<AppealApprovedDomainEvent>
{
    private readonly IIntegrationEventPublisher _events;

    public AppealApprovedEventHandler(IIntegrationEventPublisher events)
    {
        _events = events;
    }

    protected override Task HandleAsync(
        AppealApprovedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return _events.PublishAsync(
            new AppealApprovedIntegrationEvent
            {
                AppealId = domainEvent.AppealId.Value,
                UserId = domainEvent.UserId.Value,
                AppealType = domainEvent.Type.ToString(),
                TargetId = domainEvent.TargetId,
                Explanation = domainEvent.Explanation,
                DecidedAt = domainEvent.At,
                DecidedByUserId = domainEvent.DecidedBy?.Value
            },
            cancellationToken);
    }
}

public sealed class AppealRejectedEventHandler : DomainEventHandler<AppealRejectedDomainEvent>
{
    private readonly IIntegrationEventPublisher _events;

    public AppealRejectedEventHandler(IIntegrationEventPublisher events)
    {
        _events = events;
    }

    protected override Task HandleAsync(
        AppealRejectedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return _events.PublishAsync(
            new AppealRejectedIntegrationEvent
            {
                AppealId = domainEvent.AppealId.Value,
                UserId = domainEvent.UserId.Value,
                AppealType = domainEvent.Type.ToString(),
                TargetId = domainEvent.TargetId,
                Explanation = domainEvent.Explanation,
                DecidedAt = domainEvent.At
            },
            cancellationToken);
    }
}

public sealed class ModerationDecisionRecordedEventHandler
    : DomainEventHandler<ModerationDecisionRecordedDomainEvent>
{
    private readonly IIntegrationEventPublisher _events;

    public ModerationDecisionRecordedEventHandler(IIntegrationEventPublisher events)
    {
        _events = events;
    }

    protected override Task HandleAsync(
        ModerationDecisionRecordedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        // The event the owning module acts on. Trust holds the decision; only the module that
        // published a guide knows how to unpublish one.
        return _events.PublishAsync(
            new ModerationDecisionRecordedIntegrationEvent
            {
                ItemId = domainEvent.ItemId.Value,
                ModeratorId = domainEvent.ModeratorId.Value,
                ContentType = domainEvent.ContentType.ToString(),
                ContentId = domainEvent.ContentId,
                Action = domainEvent.Action.ToString(),
                DecidedAt = domainEvent.At
            },
            cancellationToken);
    }
}

/// <summary>
/// Moves a member onto the reviewer's rung when their request is picked up.
/// <para>
/// <b>The middle of three links, and the one nothing was publishing.</b> Identity's ladder is
/// <c>LimitedAccess -> VerificationPending -> VerificationUnderReview -> Verified</c> and every
/// rung is guarded: <c>Granted</c> targets <c>Verified</c>, which is reachable only from
/// <c>VerificationUnderReview</c>. Submission now puts the member on the first rung and approval
/// takes them off the third, but nothing put them on the second — so an approval would have been
/// refused by the transition table even once everything else worked.
/// </para>
/// <para>
/// Assignment raises no event of its own; it moves the request from <c>Pending</c> to
/// <c>UnderReview</c> through the ordinary status transition, which is what this reads. Any other
/// status move is somebody else's business and is ignored.
/// </para>
/// </summary>
public sealed class VerificationStatusChangedEventHandler
    : DomainEventHandler<VerificationStatusChangedDomainEvent>
{
    private readonly ITrustDecisionPublisher _decisions;

    public VerificationStatusChangedEventHandler(ITrustDecisionPublisher decisions) =>
        _decisions = decisions;

    protected override async Task HandleAsync(
        VerificationStatusChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (domainEvent.From != VerificationStatus.Pending
            || domainEvent.To != VerificationStatus.UnderReview)
        {
            return;
        }

        // The same three types that gate community access on the way in. A professional
        // credential reaching a reviewer's desk should not move somebody's whole account.
        if (domainEvent.Type is not (VerificationType.Diaspora or VerificationType.Identity
            or VerificationType.Country))
        {
            return;
        }

        await _decisions.PublishAsync(
            domainEvent.UserId,
            TrustDecisionOutcome.UnderReview,
            $"{domainEvent.Type} verification picked up for review.",
            "trust-engine",
            cancellationToken);
    }
}

/// <summary>
/// Publishes a change of a member's risk level (RWD-14, CS-4b): the level, never the score or the
/// factors, which stay staff-only. The Financial Platform holds the member's points on High.
/// </summary>
public sealed class MemberRiskLevelChangedEventHandler : DomainEventHandler<MemberRiskLevelChangedDomainEvent>
{
    private readonly IIntegrationEventPublisher _events;

    public MemberRiskLevelChangedEventHandler(IIntegrationEventPublisher events)
    {
        _events = events;
    }

    protected override async Task HandleAsync(
        MemberRiskLevelChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await _events.PublishAsync(
            new MemberRiskLevelChangedIntegrationEvent
            {
                UserId = domainEvent.UserId.Value,
                PreviousLevel = domainEvent.From.ToString(),
                CurrentLevel = domainEvent.To.ToString(),
                ChangedAt = domainEvent.At
            },
            cancellationToken);
    }
}
