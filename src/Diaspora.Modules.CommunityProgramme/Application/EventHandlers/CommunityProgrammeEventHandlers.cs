using Diaspora.Application.Abstractions;
using Diaspora.Contracts.CommunityProgramme.V1.Events;
using Diaspora.Contracts.Events;
using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Events;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Platform.Messaging;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.CommunityProgramme.Application.EventHandlers;

/// <summary>
/// Turns B18's domain events into the published integration events other modules consume.
/// <para>
/// <b>The one translation seam, and the reason B18's domain stays private.</b> A domain event carries
/// strongly typed identifiers and domain enums; an integration event carries flat primitives, so a
/// consumer can parse it without referencing this module. Doing the translation in a single class
/// means the two never drift, and that a consumer's contract cannot be changed by a refactor inside
/// an aggregate.
/// </para>
/// <para>
/// <b>A publish failure never fails the write.</b> The aggregate is already saved by the time this
/// runs; throwing here would roll back a completed decision because a notification did not go out.
/// Failures are logged, and at-least-once delivery is the contract — consumers de-duplicate on
/// <c>EventId</c>.
/// </para>
/// <para>
/// <b>Ten of the fourteen membership transitions are deliberately never published.</b> A move to
/// <c>Draft</c>, <c>Pending</c>, <c>PaymentPending</c> or <c>PaymentFailed</c> is bookkeeping the
/// member is already watching on screen, and a notification for each would make the four that matter
/// unreadable. What is published is the four an organization decided or a clock caused: approved,
/// rejected, renewal due, expired.
/// </para>
/// </summary>
public sealed class CommunityProgrammeIntegrationEventRelay
{
    private readonly IIntegrationEventPublisher _publisher;
    private readonly ILogger<CommunityProgrammeIntegrationEventRelay> _logger;

    public CommunityProgrammeIntegrationEventRelay(
        IIntegrationEventPublisher publisher,
        ILogger<CommunityProgrammeIntegrationEventRelay> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    /// <summary>The four transitions a member has to be told about.</summary>
    private static readonly ProgrammeMembershipStatus[] Announced =
    [
        ProgrammeMembershipStatus.Approved,
        ProgrammeMembershipStatus.Rejected,
        ProgrammeMembershipStatus.RenewalDue,
        ProgrammeMembershipStatus.Expired
    ];

    public Task PublishAsync(
        ProgrammeMembershipStatusChangedDomainEvent domainEvent,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (!Announced.Contains(domainEvent.CurrentStatus))
        {
            return Task.CompletedTask;
        }

        return PublishSafelyAsync(
            new ProgrammeMembershipStatusChangedIntegrationEvent
            {
                MembershipId = domainEvent.MembershipId.Value,
                OrganizationId = domainEvent.OrganizationId,
                MemberUserId = domainEvent.MemberUserId.Value,
                TierId = domainEvent.TierId.Value,
                PreviousStatus = domainEvent.PreviousStatus.ToString(),
                CurrentStatus = domainEvent.CurrentStatus.ToString(),
                Actor = domainEvent.Actor.ToString(),
                Reason = domainEvent.Reason,
                ExpiresAt = expiresAt,
                ChangedAt = domainEvent.ChangedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        VolunteerApplicationDecidedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new VolunteerApplicationDecidedIntegrationEvent
            {
                ApplicationId = domainEvent.ApplicationId.Value,
                OpportunityId = domainEvent.OpportunityId.Value,
                OrganizationId = domainEvent.OrganizationId,
                VolunteerUserId = domainEvent.VolunteerUserId.Value,
                Decision = domainEvent.CurrentStatus.ToString(),
                Reason = domainEvent.Reason,
                DecidedAt = domainEvent.DecidedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        VolunteerAssignmentCreatedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new VolunteerAssignmentCreatedIntegrationEvent
            {
                AssignmentId = domainEvent.AssignmentId.Value,
                OpportunityId = domainEvent.OpportunityId.Value,
                OrganizationId = domainEvent.OrganizationId,
                VolunteerUserId = domainEvent.VolunteerUserId.Value,
                ShiftId = domainEvent.ShiftId?.Value,
                StartsAt = domainEvent.StartsAt,
                CreatedAt = domainEvent.CreatedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        VolunteerHoursConfirmedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new VolunteerHoursConfirmedIntegrationEvent
            {
                AssignmentId = domainEvent.AssignmentId.Value,
                OpportunityId = domainEvent.OpportunityId.Value,
                OrganizationId = domainEvent.OrganizationId,
                VolunteerUserId = domainEvent.VolunteerUserId.Value,
                Hours = domainEvent.Hours,
                ConfirmedAt = domainEvent.ConfirmedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        PollStatusChangedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        // Open, closed and results-published are the three somebody outside this module can act on.
        // A draft becoming scheduled is bookkeeping.
        if (domainEvent.CurrentStatus is not (PollStatus.Open
            or PollStatus.Closed
            or PollStatus.ResultsPublished))
        {
            return Task.CompletedTask;
        }

        return PublishSafelyAsync(
            new CommunityPollStatusChangedIntegrationEvent
            {
                PollId = domainEvent.PollId.Value,
                OrganizationId = domainEvent.OrganizationId,
                Audience = domainEvent.Audience.ToString(),
                PreviousStatus = domainEvent.PreviousStatus.ToString(),
                CurrentStatus = domainEvent.CurrentStatus.ToString(),
                ChangedAt = domainEvent.ChangedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        CommunityAnnouncementPublishedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new CommunityAnnouncementPublishedIntegrationEvent
            {
                AnnouncementId = domainEvent.AnnouncementId.Value,
                Scope = domainEvent.Scope.ToString(),
                ScopeId = domainEvent.ScopeId,
                OrganizationId = domainEvent.OrganizationId,
                Title = domainEvent.Title,
                PublishedByUserId = domainEvent.PublishedBy.Value,
                PublishedAt = domainEvent.PublishedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        CommunityMilestoneReachedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new CommunityMilestoneReachedIntegrationEvent
            {
                CommunityId = domainEvent.CommunityId.Value,
                CommunityName = domainEvent.CommunityName,
                Milestone = domainEvent.Milestone,
                MemberCount = domainEvent.MemberCount,
                ReachedAt = domainEvent.OccurredAt
            },
            cancellationToken);
    }

    /// <summary>
    /// A transition into membership, for <c>community.memberJoined</c>. The visibility travels so a
    /// consumer can keep a Private member's belonging private.
    /// </summary>
    public Task PublishAsync(
        CommunityMemberJoinedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new CommunityMemberJoinedIntegrationEvent
            {
                CommunityId = domainEvent.CommunityId.Value,
                MemberId = domainEvent.MemberId.Value,
                JoinedAt = domainEvent.JoinedAt,
                JoinedHow = domainEvent.Route == CommunityJoinRoute.Approved ? "approved" : "direct",
                MemberVisibility = domainEvent.Visibility.ToString()
            },
            cancellationToken);
    }

    /// <summary>
    /// A community's owner asking for it to be closed is not published — deliberately.
    /// <para>
    /// Nobody is notified when a request is made: the members are not told (the community carries on
    /// as normal, and telling everybody it might close would end it early by itself) and Diaspora
    /// administrators read the queue rather than their inbox. What is published is what was
    /// <em>decided</em>, and what finally happened.
    /// </para>
    /// </summary>
    public Task PublishAsync(
        CommunityDeletionDecidedDomainEvent domainEvent,
        string communitySlug,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new CommunityDeletionDecidedIntegrationEvent
            {
                RequestId = domainEvent.RequestId.Value,
                CommunityId = domainEvent.CommunityId.Value,
                CommunityName = domainEvent.CommunityName,
                CommunitySlug = communitySlug,
                OwnerUserId = domainEvent.OwnerUserId.Value,
                Approved = domainEvent.Approved,
                DecisionReason = domainEvent.DecisionReason,
                EffectiveAt = domainEvent.EffectiveAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        CommunityDeletedDomainEvent domainEvent,
        string communitySlug,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new CommunityDeletedIntegrationEvent
            {
                CommunityId = domainEvent.CommunityId.Value,
                CommunityName = domainEvent.CommunityName,
                CommunitySlug = communitySlug,
                OwnerUserId = domainEvent.ClosedFor.Value,
                Reason = domainEvent.Reason,
                ClosedAt = domainEvent.OccurredAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        CommunityOwnershipOfferedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new CommunityOwnershipOfferedIntegrationEvent
            {
                TransferId = domainEvent.TransferId.Value,
                CommunityId = domainEvent.CommunityId.Value,
                CommunityName = domainEvent.CommunityName,
                CommunitySlug = domainEvent.CommunitySlug,
                OfferedByUserId = domainEvent.OfferedBy.Value,
                OfferedToUserId = domainEvent.OfferedTo.Value,
                Message = domainEvent.Message,
                ExpiresAt = domainEvent.ExpiresAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        CommunityOwnershipDecidedDomainEvent domainEvent,
        string communitySlug,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new CommunityOwnershipDecidedIntegrationEvent
            {
                TransferId = domainEvent.TransferId.Value,
                CommunityId = domainEvent.CommunityId.Value,
                CommunityName = domainEvent.CommunityName,
                CommunitySlug = communitySlug,
                OfferedByUserId = domainEvent.OfferedBy.Value,
                OfferedToUserId = domainEvent.OfferedTo.Value,
                Accepted = domainEvent.Accepted,
                DeclineReason = domainEvent.DeclineReason
            },
            cancellationToken);
    }

    private async Task PublishSafelyAsync(
        IIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        try
        {
            await _publisher.PublishAsync(integrationEvent, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Logged, never rethrown. See the class summary: the write is already committed.
            _logger.LogError(
                exception,
                "Failed to publish {IntegrationEvent} from the Community Programme module.",
                integrationEvent.GetType().Name);
        }
    }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════
// The handlers.
//
// One per domain event — DSP1002 reports a second — and each does exactly one thing: hand the fact
// to the relay. Anything more would be work running inside the transaction that raised the event,
// which is the pipeline this platform reserves for facts that must not be lost.
// ════════════════════════════════════════════════════════════════════════════════════════════════

public sealed class ProgrammeMembershipStatusChangedEventHandler
    : DomainEventHandler<ProgrammeMembershipStatusChangedDomainEvent>
{
    private readonly CommunityProgrammeIntegrationEventRelay _relay;
    private readonly IProgrammeMembershipRepository _memberships;

    public ProgrammeMembershipStatusChangedEventHandler(
        CommunityProgrammeIntegrationEventRelay relay,
        IProgrammeMembershipRepository memberships)
    {
        _relay = relay;
        _memberships = memberships;
    }

    protected override async Task HandleAsync(
        ProgrammeMembershipStatusChangedDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        // The expiry is read off the aggregate rather than carried on the domain event, because the
        // event is raised inside MoveTo and the window is set by the same call — the read is the
        // identity map rather than a round trip.
        var membership = await _memberships.GetAsync(domainEvent.MembershipId, cancellationToken);

        await _relay.PublishAsync(domainEvent, membership?.ExpiresAt, cancellationToken);
    }
}

public sealed class VolunteerApplicationDecidedEventHandler
    : DomainEventHandler<VolunteerApplicationDecidedDomainEvent>
{
    private readonly CommunityProgrammeIntegrationEventRelay _relay;

    public VolunteerApplicationDecidedEventHandler(
        CommunityProgrammeIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        VolunteerApplicationDecidedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}

public sealed class VolunteerAssignmentCreatedEventHandler
    : DomainEventHandler<VolunteerAssignmentCreatedDomainEvent>
{
    private readonly CommunityProgrammeIntegrationEventRelay _relay;

    public VolunteerAssignmentCreatedEventHandler(
        CommunityProgrammeIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        VolunteerAssignmentCreatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}

public sealed class VolunteerHoursConfirmedEventHandler
    : DomainEventHandler<VolunteerHoursConfirmedDomainEvent>
{
    private readonly CommunityProgrammeIntegrationEventRelay _relay;

    public VolunteerHoursConfirmedEventHandler(
        CommunityProgrammeIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        VolunteerHoursConfirmedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}

public sealed class PollStatusChangedEventHandler
    : DomainEventHandler<PollStatusChangedDomainEvent>
{
    private readonly CommunityProgrammeIntegrationEventRelay _relay;

    public PollStatusChangedEventHandler(
        CommunityProgrammeIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        PollStatusChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}

public sealed class CommunityAnnouncementPublishedEventHandler
    : DomainEventHandler<CommunityAnnouncementPublishedDomainEvent>
{
    private readonly CommunityProgrammeIntegrationEventRelay _relay;

    public CommunityAnnouncementPublishedEventHandler(
        CommunityProgrammeIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        CommunityAnnouncementPublishedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}

public sealed class CommunityMemberJoinedEventHandler
    : DomainEventHandler<CommunityMemberJoinedDomainEvent>
{
    private readonly CommunityProgrammeIntegrationEventRelay _relay;

    public CommunityMemberJoinedEventHandler(
        CommunityProgrammeIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        CommunityMemberJoinedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}

public sealed class CommunityMilestoneReachedEventHandler
    : DomainEventHandler<CommunityMilestoneReachedDomainEvent>
{
    private readonly CommunityProgrammeIntegrationEventRelay _relay;

    public CommunityMilestoneReachedEventHandler(
        CommunityProgrammeIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        CommunityMilestoneReachedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}


// ── Closing a community, and handing one over — 11 Sep 2026 ────────────────────────────
//
// Each of these reads the community for one field the domain event does not carry: the slug, which
// is what a notification links to. The alternative was putting the slug on every domain event, and a
// domain event is not the place to carry a field only a deep link needs.

public sealed class CommunityDeletionDecidedEventHandler
    : DomainEventHandler<CommunityDeletionDecidedDomainEvent>
{
    private readonly CommunityProgrammeIntegrationEventRelay _relay;
    private readonly ICommunityRepository _communities;

    public CommunityDeletionDecidedEventHandler(
        CommunityProgrammeIntegrationEventRelay relay, ICommunityRepository communities)
    {
        _relay = relay;
        _communities = communities;
    }

    protected override async Task HandleAsync(
        CommunityDeletionDecidedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var community = await _communities.GetAsync(domainEvent.CommunityId, cancellationToken);

        await _relay.PublishAsync(
            domainEvent, community?.Slug.Value ?? string.Empty, cancellationToken);
    }
}

public sealed class CommunityDeletedEventHandler : DomainEventHandler<CommunityDeletedDomainEvent>
{
    private readonly CommunityProgrammeIntegrationEventRelay _relay;
    private readonly ICommunityRepository _communities;

    public CommunityDeletedEventHandler(
        CommunityProgrammeIntegrationEventRelay relay, ICommunityRepository communities)
    {
        _relay = relay;
        _communities = communities;
    }

    protected override async Task HandleAsync(
        CommunityDeletedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var community = await _communities.GetAsync(domainEvent.CommunityId, cancellationToken);

        await _relay.PublishAsync(
            domainEvent, community?.Slug.Value ?? string.Empty, cancellationToken);
    }
}

public sealed class CommunityOwnershipOfferedEventHandler
    : DomainEventHandler<CommunityOwnershipOfferedDomainEvent>
{
    private readonly CommunityProgrammeIntegrationEventRelay _relay;

    public CommunityOwnershipOfferedEventHandler(
        CommunityProgrammeIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        CommunityOwnershipOfferedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}

public sealed class CommunityOwnershipDecidedEventHandler
    : DomainEventHandler<CommunityOwnershipDecidedDomainEvent>
{
    private readonly CommunityProgrammeIntegrationEventRelay _relay;
    private readonly ICommunityRepository _communities;

    public CommunityOwnershipDecidedEventHandler(
        CommunityProgrammeIntegrationEventRelay relay, ICommunityRepository communities)
    {
        _relay = relay;
        _communities = communities;
    }

    protected override async Task HandleAsync(
        CommunityOwnershipDecidedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var community = await _communities.GetAsync(domainEvent.CommunityId, cancellationToken);

        await _relay.PublishAsync(
            domainEvent, community?.Slug.Value ?? string.Empty, cancellationToken);
    }
}
