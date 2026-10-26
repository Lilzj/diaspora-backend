using Diaspora.Contracts.CommunityLife.V1.Events;
using Diaspora.Contracts.Content.V1.Events;
using Diaspora.Contracts.Events;
using Diaspora.Modules.CommunityLife.Domain.Catalog;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Events;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.CommunityLife.Domain.Series;
using Diaspora.Platform.Messaging;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.CommunityLife.Application.EventHandlers;

/// <summary>
/// Turns B12's domain events into the published integration events other modules consume.
/// <para>
/// <b>The one translation seam, and the reason B12's domain stays private.</b> A domain event carries
/// strongly typed identifiers and domain enums; an integration event carries flat primitives, so a
/// consumer can parse it without referencing this module. Doing the translation in a single class
/// means the two never drift, and means a consumer's contract cannot be changed by a refactor inside
/// an aggregate.
/// </para>
/// <para>
/// <b>A publish failure never fails the write.</b> The aggregate is already saved by the time this
/// runs; throwing here would roll back a completed check-in because a notification did not go out.
/// Failures are logged, and at-least-once delivery is the contract — consumers treat <c>EventId</c>
/// as the idempotency key. Which is also why no payload here declares a property called
/// <c>EventId</c>: the base <c>IntegrationEvent</c> already owns that name, and an event identifier
/// shadowing the de-duplication key would silently break every consumer's idempotency. The event's
/// own identifier is <c>CommunityEventId</c> throughout.
/// </para>
/// <para>
/// <b>Four domain events are deliberately never published.</b> A verification <em>request</em> is
/// Trust's to announce, not this module's — publishing it would tell every subscriber that a body is
/// under review before anybody has decided anything. A membership <em>role</em> change is internal
/// bookkeeping inside one organization and of no use to another module. A media <em>detachment</em>
/// is the absence of a fact rather than a fact. And a restore-from-archive is followed by a
/// publication if it matters, so announcing it separately would double-count.
/// </para>
/// </summary>
public sealed class CommunityLifeIntegrationEventRelay
{
    private readonly IIntegrationEventPublisher _publisher;
    private readonly ICommunityEventRepository _events;
    private readonly IEventSeriesRepository _series;
    private readonly IOrganizationRepository _organizations;
    private readonly IEventCategoryRepository _categories;
    private readonly ILogger<CommunityLifeIntegrationEventRelay> _logger;

    public CommunityLifeIntegrationEventRelay(
        IIntegrationEventPublisher publisher,
        ICommunityEventRepository events,
        IEventSeriesRepository series,
        IOrganizationRepository organizations,
        IEventCategoryRepository categories,
        ILogger<CommunityLifeIntegrationEventRelay> logger)
    {
        _publisher = publisher;
        _events = events;
        _series = series;
        _organizations = organizations;
        _categories = categories;
        _logger = logger;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Organization lifecycle
    // ════════════════════════════════════════════════════════════════════════════════════════

    public Task PublishAsync(
        OrganizationCreatedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new OrganizationCreatedIntegrationEvent
            {
                OrganizationId = domainEvent.OrganizationId.Value,
                CreatedByUserId = domainEvent.CreatedBy.Value,
                Name = domainEvent.Name,
                Slug = domainEvent.Slug,
                OrganizationTypeKey = domainEvent.OrganizationTypeKey,
                CountryCode = domainEvent.CountryCode,
                NeighborhoodAreaId = domainEvent.NeighborhoodAreaId,
                CreatedAt = domainEvent.OccurredAt
            },
            cancellationToken);
    }

    /// <summary>
    /// An organization becoming visible. Reads the body back so the payload carries everything a
    /// card-level index needs.
    /// <para>
    /// The read is inline, inside the same unit of work that raised the event, so it is the identity
    /// map rather than a round trip.
    /// </para>
    /// </summary>
    public async Task PublishAsync(
        OrganizationPublishedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var organization = await _organizations.GetAsync(
            domainEvent.OrganizationId, cancellationToken);

        if (organization is null)
        {
            return;
        }

        await PublishLiveAsync(
            organization, domainEvent.IsFirstPublication, domainEvent.PublishedAt, cancellationToken);
    }

    /// <summary>
    /// An organization becoming visible, read entirely off the aggregate.
    /// <para>
    /// <b>Reachable from three places</b>: a first publication, a republication, and a suspension
    /// being lifted. That last one is the reason this is not inlined — an organization whose
    /// suspension was cleared raises only a status change, so without this a body the platform had
    /// stopped vouching for and then reinstated would stay de-indexed in every consumer forever.
    /// </para>
    /// </summary>
    private Task PublishLiveAsync(
        Organization organization,
        bool isFirstPublication,
        DateTimeOffset publishedAt,
        CancellationToken cancellationToken) =>
        PublishSafelyAsync(
            new OrganizationPublishedIntegrationEvent
            {
                OrganizationId = organization.Id.Value,
                Name = organization.Name.Value,
                ShortName = organization.Name.ShortName,
                Slug = organization.Slug.Value,
                OrganizationTypeKey = organization.OrganizationTypeKey,
                Summary = organization.Description.Summary,
                CountryCode = organization.Location.CountryCode,
                RegionName = organization.Location.RegionName,
                CityName = organization.Location.CityName,
                NeighborhoodName = organization.Location.NeighborhoodName,
                NeighborhoodAreaId = organization.Location.NeighborhoodAreaId,
                LogoMediaId = organization.Logo?.MediaId,
                CoverMediaId = organization.CoverImage?.MediaId,
                Languages = [.. organization.Languages.Select(language => language.Name)],
                IsVerified = organization.TrustMark.IsVerified,
                MemberCount = organization.Statistics.MemberCount,
                IsFirstPublication = isFirstPublication,
                PublishedAt = publishedAt,
                OrganizerUserId = organization.CreatedByUserId.Value
            },
            cancellationToken);

    /// <summary>
    /// An organization leaving circulation, or coming back into it.
    /// <para>
    /// <b>This is why the aggregate raises one status-changed event rather than five.</b> Archival,
    /// suspension and deletion are three routes to the same consumer obligation — stop showing this —
    /// so they share one contract and differ only in <c>CurrentState</c> and whether a moderator did
    /// it. A consumer that de-indexes on all three needs one subscription, not three.
    /// </para>
    /// <para>
    /// <b>A suspension is deliberately not published from here.</b> Suspending a body raises a
    /// moderation change <em>and</em> a status change — the aggregate does both, so that its events
    /// stop appearing too — and publishing from both would put the same archival on the bus twice
    /// under two different <c>EventId</c>s, which is precisely the shape consumer de-duplication
    /// cannot catch. The moderation overload owns it, and labels it as the moderator's act.
    /// </para>
    /// </summary>
    public async Task PublishAsync(
        OrganizationStatusChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (domainEvent.CurrentStatus == OrganizationStatus.Archived)
        {
            await PublishArchivedAsync(
                domainEvent.OrganizationId,
                domainEvent.CurrentStatus.ToString(),
                isModerationAction: false,
                domainEvent.Reason,
                domainEvent.ChangedAt,
                cancellationToken);

            return;
        }

        // Coming back from a suspension. Nothing else announces it, and a consumer that de-indexed
        // on the suspension has no other way to learn the body is live again.
        if (domainEvent.PreviousStatus != OrganizationStatus.Suspended
            || domainEvent.CurrentStatus is not (OrganizationStatus.Published
                or OrganizationStatus.Active))
        {
            return;
        }

        var organization = await _organizations.GetAsync(
            domainEvent.OrganizationId, cancellationToken);

        if (organization is not null)
        {
            await PublishLiveAsync(
                organization,
                isFirstPublication: false,
                domainEvent.ChangedAt,
                cancellationToken);
        }
    }

    public Task PublishAsync(
        OrganizationDeletedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishArchivedAsync(
            domainEvent.OrganizationId,
            "Deleted",
            isModerationAction: false,
            reason: null,
            domainEvent.DeletedAt,
            cancellationToken);
    }

    /// <summary>
    /// A moderator acting on a body.
    /// <para>
    /// A hide or a suspension de-indexes it; clearing one puts it back, which is published as an
    /// ordinary republication rather than as a "this was cleared" — a subscriber does not need to
    /// learn that a body it can see was once under suspicion. A flag is not published at all: it is
    /// a queue entry for a moderator, and the page is still live while somebody looks at it.
    /// </para>
    /// </summary>
    public async Task PublishAsync(
        OrganizationModerationChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (domainEvent.CurrentState is CommunityLifeModerationState.Hidden
            or CommunityLifeModerationState.Suspended)
        {
            await PublishArchivedAsync(
                domainEvent.OrganizationId,
                domainEvent.CurrentState.ToString(),
                isModerationAction: true,
                domainEvent.Reason,
                domainEvent.OccurredAt,
                cancellationToken);

            return;
        }

        if (domainEvent.CurrentState != CommunityLifeModerationState.Clear
            || domainEvent.PreviousState is not (CommunityLifeModerationState.Hidden
                or CommunityLifeModerationState.Suspended))
        {
            return;
        }

        var organization = await _organizations.GetAsync(
            domainEvent.OrganizationId, cancellationToken);

        // A cleared suspension also moves the lifecycle back to published, and the status overload
        // announces that. Republishing here as well would double it, so this covers only the hide,
        // which leaves the status where it was.
        if (organization is { Status: OrganizationStatus.Published or OrganizationStatus.Active }
            && domainEvent.PreviousState == CommunityLifeModerationState.Hidden)
        {
            await PublishLiveAsync(
                organization,
                isFirstPublication: false,
                domainEvent.OccurredAt,
                cancellationToken);
        }
    }

    private async Task PublishArchivedAsync(
        OrganizationId organizationId,
        string currentState,
        bool isModerationAction,
        string? reason,
        DateTimeOffset archivedAt,
        CancellationToken cancellationToken)
    {
        var organization = await _organizations.GetAsync(organizationId, cancellationToken);

        await PublishSafelyAsync(
            new OrganizationArchivedIntegrationEvent
            {
                OrganizationId = organizationId.Value,
                Slug = organization?.Slug.Value ?? string.Empty,
                CurrentState = currentState,
                IsModerationAction = isModerationAction,
                Reason = reason,
                ArchivedAt = archivedAt,
                OrganizerUserId = organization?.CreatedByUserId.Value
            },
            cancellationToken);
    }

    /// <summary>
    /// Trust's decision on a body, restated in this module's vocabulary.
    /// <para>
    /// Published on every outcome rather than only on approval, because a revocation is the one a
    /// consumer most needs: a cached badge that outlives the verification behind it is exactly the
    /// signal a member would act on wrongly.
    /// </para>
    /// </summary>
    public async Task PublishAsync(
        OrganizationVerificationChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var organization = await _organizations.GetAsync(
            domainEvent.OrganizationId, cancellationToken);

        await PublishSafelyAsync(
            new OrganizationVerifiedIntegrationEvent
            {
                OrganizationId = domainEvent.OrganizationId.Value,
                Name = organization?.Name.Value ?? string.Empty,
                OrganizationTypeKey = organization?.OrganizationTypeKey ?? string.Empty,
                PreviousState = domainEvent.PreviousState.ToString(),
                CurrentState = domainEvent.CurrentState.ToString(),
                IsVerified =
                    domainEvent.CurrentState == OrganizationVerificationState.Verified,
                VerificationId = domainEvent.VerificationId,
                Badge = domainEvent.Badge,
                Reason = domainEvent.Reason,
                ChangedAt = domainEvent.ChangedAt,

                // The body is already loaded above for its name. Trust decided this and the
                // registered administrator is the one person it is about.
                OrganizerUserId = organization?.CreatedByUserId.Value
            },
            cancellationToken);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Membership
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A membership moving, translated into whichever published contract it corresponds to.
    /// <para>
    /// <b>A request and an invitation are deliberately not published.</b> Somebody asking to join a
    /// political association, a support group or a place of worship is not a fact that belongs on a
    /// bus before that body has agreed to it. Admission is, because by then both sides have.
    /// </para>
    /// <para>
    /// <b>The member count is passed in rather than read back.</b> The handler settles the
    /// organization's counters before calling this, and reading them here would make the payload
    /// depend on the order of two lines in a different file.
    /// </para>
    /// </summary>
    public async Task PublishAsync(
        OrganizationMembershipStatusChangedDomainEvent domainEvent,
        bool isMembershipPublic,
        int memberCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (domainEvent.CurrentStatus == MembershipStatus.Active)
        {
            await PublishSafelyAsync(
                new OrganizationMemberJoinedIntegrationEvent
                {
                    MembershipId = domainEvent.MembershipId.Value,
                    OrganizationId = domainEvent.OrganizationId.Value,
                    MemberUserId = domainEvent.MemberUserId.Value,
                    Role = domainEvent.Role.ToString(),
                    IsMembershipPublic = isMembershipPublic,
                    MemberCount = memberCount,
                    JoinedAt = domainEvent.ChangedAt,
                    OrganizerUserId =
                        await AdministratorOfAsync(domainEvent.OrganizationId, cancellationToken)
                },
                cancellationToken);

            return;
        }

        if (domainEvent.PreviousStatus != MembershipStatus.Active
            || domainEvent.CurrentStatus is not (MembershipStatus.Left
                or MembershipStatus.Removed
                or MembershipStatus.Lapsed))
        {
            return;
        }

        await PublishSafelyAsync(
            new OrganizationMemberLeftIntegrationEvent
            {
                MembershipId = domainEvent.MembershipId.Value,
                OrganizationId = domainEvent.OrganizationId.Value,
                MemberUserId = domainEvent.MemberUserId.Value,
                WasRemoved = domainEvent.CurrentStatus == MembershipStatus.Removed,
                WasLapsed = domainEvent.CurrentStatus == MembershipStatus.Lapsed,
                MemberCount = memberCount,
                LeftAt = domainEvent.ChangedAt,
                OrganizerUserId =
                    await AdministratorOfAsync(domainEvent.OrganizationId, cancellationToken)
            },
            cancellationToken);
    }

    /// <summary>
    /// The organization's registered administrator, or null when the body is gone.
    /// <para>
    /// <b>The member who created it, and deliberately not a fan-out across its leadership.</b>
    /// Membership is its own aggregate here, so "the committee" is a query with no bound on its
    /// result — B12 was explicit that an organization with forty thousand members must not be
    /// able to generate forty thousand messages, and B13.1 does not change that. One addressable
    /// person crosses the boundary; who else should hear about it is a decision for whatever
    /// module later grows an opinion about organization roles.
    /// </para>
    /// </summary>
    private async Task<Guid?> AdministratorOfAsync(
        OrganizationId organizationId, CancellationToken cancellationToken)
    {
        var organization = await _organizations.GetAsync(organizationId, cancellationToken);

        return organization?.CreatedByUserId.Value;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Event lifecycle
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <param name="seriesId">
    /// The repeating event that generated it, when one did — B12.1. Passed in rather than read
    /// back: the creation handler already holds the series, and this path runs once per occurrence
    /// during an expansion that can write sixty of them.
    /// </param>
    /// <param name="seriesSequence">Its ordinal in that series.</param>
    public Task PublishAsync(
        CommunityEventCreatedDomainEvent domainEvent,
        Guid? seriesId = null,
        int? seriesSequence = null,
        Guid? communityId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new EventCreatedIntegrationEvent
            {
                SeriesId = seriesId,
                SeriesSequence = seriesSequence,
                CommunityEventId = domainEvent.CommunityEventId.Value,
                OrganizationId = domainEvent.OrganizationId?.Value,
                OrganizerUserId = domainEvent.CreatedBy.Value,
                Title = domainEvent.Title,
                Slug = domainEvent.Slug,
                CategoryKey = domainEvent.CategoryKey,
                CountryCode = domainEvent.CountryCode,
                NeighborhoodAreaId = domainEvent.NeighborhoodAreaId,
                StartsAt = domainEvent.StartsAt,
                CreatedAt = domainEvent.OccurredAt,
                CommunityId = communityId
            },
            cancellationToken);
    }

    /// <summary>
    /// The module's most consequential contract. Reads the event, its host and its category back so
    /// a consumer can index a card, place a pin and filter a facet without a single call back here.
    /// <para>
    /// <b><c>IsOrganizerVerified</c> means the host body is verified, not the member.</b> B12 has no
    /// per-member trust lookup and inventing one here would put a badge on a person from a module
    /// that never asked Trust about them. An event hosted by one neighbour inviting others reports
    /// false, which is honest: nobody has vouched for it.
    /// </para>
    /// </summary>
    public async Task PublishAsync(
        CommunityEventPublishedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var communityEvent = await _events.GetAsync(
            domainEvent.CommunityEventId, cancellationToken);

        if (communityEvent is null)
        {
            return;
        }

        await PublishLiveAsync(
            communityEvent,
            domainEvent.IsFirstPublication,
            domainEvent.PublishedAt,
            cancellationToken);
    }

    /// <summary>
    /// An event becoming visible, read entirely off the aggregate.
    /// <para>
    /// <b><c>IsOrganizerVerified</c> means the host body is verified, not the member.</b> B12 has no
    /// per-member trust lookup and inventing one here would put a badge on a person from a module
    /// that never asked Trust about them. An event hosted by one neighbour inviting others reports
    /// false, which is honest: nobody has vouched for it.
    /// </para>
    /// <para>
    /// <b>Reachable from a republication as well as a first publication</b>, including a hide being
    /// lifted — nothing else announces that, and a consumer that de-indexed on the hide would
    /// otherwise never learn the event is back.
    /// </para>
    /// </summary>
    private async Task PublishLiveAsync(
        CommunityEvent communityEvent,
        bool isFirstPublication,
        DateTimeOffset publishedAt,
        CancellationToken cancellationToken)
    {
        var organization = communityEvent.OrganizationId is { } organizationId
            ? await _organizations.GetAsync(organizationId, cancellationToken)
            : null;

        await PublishSafelyAsync(
            new EventPublishedIntegrationEvent
            {
                CommunityEventId = communityEvent.Id.Value,
                OrganizationId = communityEvent.OrganizationId?.Value,
                OrganizationName = organization?.Name.Value,
                OrganizerUserId = communityEvent.OrganizerUserId.Value,
                IsOrganizerVerified = organization?.TrustMark.IsVerified ?? false,
                Title = communityEvent.Title.Value,
                Slug = communityEvent.Slug.Value,
                Summary = communityEvent.Description.Summary,
                CategoryKey = communityEvent.CategoryKey,
                Facets = await FacetsForAsync(communityEvent.CategoryKey, cancellationToken),
                StartsAt = communityEvent.Schedule.StartsAt,
                EndsAt = communityEvent.Schedule.EndsAt,
                TimeZoneId = communityEvent.Schedule.TimeZoneId,
                AttendanceMode = communityEvent.AttendanceMode.ToString(),
                VenueName = communityEvent.Venue.Name,
                CountryCode = communityEvent.Location.CountryCode,
                RegionName = communityEvent.Location.RegionName,
                CityName = communityEvent.Location.CityName,
                NeighborhoodName = communityEvent.Location.NeighborhoodName,
                NeighborhoodAreaId = communityEvent.Location.NeighborhoodAreaId,
                Latitude = communityEvent.Venue.Latitude,
                Longitude = communityEvent.Venue.Longitude,
                CoverMediaId = communityEvent.CoverImage?.MediaId,
                IsFree = communityEvent.Audience.IsFree,
                IsFamilyFriendly = communityEvent.Audience.IsFamilyFriendly,
                IsChildFriendly = communityEvent.Audience.IsChildFriendly,
                IsStudentFocused = communityEvent.Audience.IsStudentFocused,
                RequiresRsvp = communityEvent.RequiresRsvp,
                Capacity = communityEvent.Capacity.Maximum,
                IsFirstPublication = isFirstPublication,
                PublishedAt = publishedAt,

                // B12.1. A search index that groups a weekly service into one card rather than
                // twenty-six needs this, and cannot derive it from anything else on the payload.
                SeriesId = communityEvent.SeriesId?.Value,
                SeriesSequence = communityEvent.SeriesSequence,

                // 21 Oct 2026. Where the host posted it, for community.eventCreated.
                CommunityId = communityEvent.CommunityId,

                // 21 Oct 2026. Who may be told about it by somebody other than its host.
                IsListedPublicly = communityEvent.IsListedPublicly
            },
            cancellationToken);
    }

    /// <summary>
    /// An event's content changed.
    /// <para>
    /// <b>B12.1, and it closes a gap open since B12 shipped.</b> The aggregate has raised this
    /// domain event on every content change from the beginning and nothing translated it, so no
    /// consumer outside the module could learn that an event had been edited — a saved search kept a
    /// stale title, and an automation platform had nothing to trigger on. The fact existed; the
    /// contract did not.
    /// </para>
    /// <para>
    /// Reads the event back for the fields a consumer needs to act — the new times, and how many
    /// people are affected — because a payload of field <em>names</em> alone would send every
    /// consumer straight back here.
    /// </para>
    /// </summary>
    public async Task PublishAsync(
        CommunityEventUpdatedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var communityEvent = await _events.GetAsync(
            domainEvent.CommunityEventId, cancellationToken);

        // A draft nobody can see has no consumer with an opinion about it being edited. Publishing
        // every keystroke of an unannounced event would be noise on the bus and a notification for
        // something that does not exist yet.
        if (communityEvent is null
            || communityEvent.Status is EventStatus.Draft or EventStatus.Archived)
        {
            return;
        }

        await PublishSafelyAsync(
            new EventUpdatedIntegrationEvent
            {
                CommunityEventId = communityEvent.Id.Value,
                OrganizationId = communityEvent.OrganizationId?.Value,
                SeriesId = communityEvent.SeriesId?.Value,
                Title = communityEvent.Title.Value,
                Slug = communityEvent.Slug.Value,
                StartsAt = communityEvent.Schedule.StartsAt,
                EndsAt = communityEvent.Schedule.EndsAt,
                TimeZoneId = communityEvent.Schedule.TimeZoneId,
                ChangedFields = domainEvent.ChangedFields,
                IsMaterialChange = domainEvent.IsMaterialChange,
                AffectedAttendeeCount = communityEvent.Capacity.GoingCount,
                ChangedByUserId = domainEvent.ChangedBy?.Value,
                OrganizerUserId = communityEvent.OrganizerUserId.Value,
                UpdatedAt = domainEvent.OccurredAt,
                IsListedPublicly = communityEvent.IsListedPublicly
            },
            cancellationToken);
    }

    /// <summary>
    /// One date of a repeating event moved off the slot its rule generated.
    /// <para>
    /// Published in addition to the ordinary update, not instead of it: a consumer that only knows
    /// about single events still needs to hear that this one moved, and a consumer that understands
    /// series needs to hear that the <em>pattern</em> did not. Two contracts, because they are two
    /// different sentences to a member.
    /// </para>
    /// </summary>
    public async Task PublishAsync(
        EventOccurrenceRescheduledDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var occurrence = await _events.GetAsync(
            domainEvent.CommunityEventId, cancellationToken);

        if (occurrence is null)
        {
            return;
        }

        await PublishSafelyAsync(
            new EventOccurrenceRescheduledIntegrationEvent
            {
                CommunityEventId = domainEvent.CommunityEventId.Value,
                SeriesId = domainEvent.EventSeriesId.Value,
                OrganizationId = domainEvent.OrganizationId?.Value,
                Sequence = occurrence.SeriesSequence ?? 0,
                Title = occurrence.Title.Value,
                Slug = occurrence.Slug.Value,
                OriginalStartsAt = domainEvent.OriginalStartsAt,
                PreviousStartsAt = domainEvent.PreviousStartsAt,
                CurrentStartsAt = domainEvent.CurrentStartsAt,
                CurrentEndsAt = occurrence.Schedule.EndsAt,
                TimeZoneId = occurrence.Schedule.TimeZoneId,
                AffectedAttendeeCount = domainEvent.AffectedAttendeeCount,
                ChangedByUserId = domainEvent.ChangedBy?.Value,
                OrganizerUserId = occurrence.OrganizerUserId.Value,
                RescheduledAt = domainEvent.ChangedAt,
                IsListedPublicly = occurrence.IsListedPublicly
            },
            cancellationToken);
    }

    /// <summary>
    /// An event called off. Carries the number of people it affects, because that is the figure a
    /// notification consumer sizes its work from before reading anything back.
    /// <para>
    /// <b>Whether a moderator did it is derived rather than passed.</b> A cancellation from a body
    /// whose event is hidden or suspended is a moderation action however it was invoked, and reading
    /// it off the aggregate means no caller can mislabel it.
    /// </para>
    /// </summary>
    public async Task PublishAsync(
        CommunityEventCancelledDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var communityEvent = await _events.GetAsync(
            domainEvent.CommunityEventId, cancellationToken);

        await PublishSafelyAsync(
            new EventCancelledIntegrationEvent
            {
                CommunityEventId = domainEvent.CommunityEventId.Value,
                OrganizationId = domainEvent.OrganizationId?.Value,
                Title = domainEvent.Title,
                Slug = communityEvent?.Slug.Value ?? string.Empty,
                StartsAt = domainEvent.StartsAt,
                AffectedAttendeeCount = domainEvent.AffectedAttendeeCount,
                IsModerationAction =
                    communityEvent?.ModerationState is CommunityLifeModerationState.Hidden
                        or CommunityLifeModerationState.Suspended,
                Reason = domainEvent.Reason,
                CancelledAt = domainEvent.CancelledAt,

                // Read off the aggregate that is already loaded above. A moderator's cancellation
                // is the case this exists for.
                OrganizerUserId = communityEvent?.OrganizerUserId.Value,

                // B12.1. What lets a consumer say "this week's meetup is off" rather than "the
                // meetup is off". The second sentence, said about one date, empties a room for the
                // rest of the year.
                SeriesId = communityEvent?.SeriesId?.Value,

                // Whether the pattern itself went. Derived rather than passed: a series that has
                // been called off is the only thing that makes a date's cancellation part of a
                // wider one, and reading it off the series' own state means no caller can mislabel
                // it.
                IsSeriesCancellation = communityEvent?.SeriesId is { } seriesId
                    && await SeriesIsCancelledAsync(seriesId, cancellationToken),

                // Read off the domain event rather than the aggregate: by now it is Cancelled, and
                // what matters is whether strangers could see it a moment before.
                IsListedPublicly = domainEvent.WasListedPublicly
            },
            cancellationToken);
    }

    /// <summary>
    /// Whether the pattern behind a cancelled date has itself been called off.
    /// <para>
    /// Read rather than passed in, so the flag cannot disagree with the series. Null-safe: a
    /// cancellation racing a series deletion reports false, which is the safer of the two wrong
    /// answers — "this date is off" understates rather than overstates.
    /// </para>
    /// </summary>
    private async Task<bool> SeriesIsCancelledAsync(
        EventSeriesId seriesId, CancellationToken cancellationToken)
    {
        var series = await _series.GetAsync(seriesId, cancellationToken);

        return series?.Status == EventSeriesStatus.Cancelled;
    }

    public async Task PublishAsync(
        CommunityEventCompletedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var communityEvent = await _events.GetAsync(
            domainEvent.CommunityEventId, cancellationToken);

        await PublishSafelyAsync(
            new EventCompletedIntegrationEvent
            {
                CommunityEventId = domainEvent.CommunityEventId.Value,
                OrganizationId = domainEvent.OrganizationId?.Value,
                OrganizerUserId = communityEvent?.OrganizerUserId.Value ?? Guid.Empty,
                CategoryKey = domainEvent.CategoryKey,
                CountryCode = communityEvent?.Location.CountryCode ?? string.Empty,
                NeighborhoodAreaId = communityEvent?.Location.NeighborhoodAreaId,
                GoingCount = domainEvent.GoingCount,
                CheckedInCount = domainEvent.CheckedInCount,
                NoShowCount = domainEvent.NoShowCount,
                AttendanceRate = domainEvent.GoingCount == 0
                    ? null
                    : (double)domainEvent.CheckedInCount / domainEvent.GoingCount,
                CompletedAt = domainEvent.CompletedAt
            },
            cancellationToken);
    }

    /// <summary>
    /// An event leaving circulation. See the organization equivalent for why archival, suspension
    /// and deletion share one contract.
    /// </summary>
    public Task PublishAsync(
        CommunityEventStatusChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        // An event taken back to draft has left circulation as surely as an archived one — 21 Oct
        // 2026. Nothing announced it, so a reminder armed for anybody watching would have gone on to
        // announce a draft. Draft from a live state only: an archived event restored to draft was
        // already announced as gone.
        var unpublished = domainEvent.CurrentStatus == EventStatus.Draft
            && domainEvent.PreviousStatus is EventStatus.Published or EventStatus.InProgress;

        if (domainEvent.CurrentStatus != EventStatus.Archived && !unpublished)
        {
            return Task.CompletedTask;
        }

        return PublishArchivedAsync(
            domainEvent.CommunityEventId,
            domainEvent.OrganizationId,
            domainEvent.CurrentStatus.ToString(),
            isModerationAction: false,
            domainEvent.Reason,
            domainEvent.ChangedAt,
            cancellationToken);
    }

    public Task PublishAsync(
        CommunityEventDeletedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishArchivedAsync(
            domainEvent.CommunityEventId,
            domainEvent.OrganizationId,
            "Deleted",
            isModerationAction: false,
            reason: null,
            domainEvent.DeletedAt,
            cancellationToken);
    }

    /// <summary>
    /// A moderator acting on an event. Clearing a hide or a suspension republishes it, for the same
    /// reason as the organization equivalent — nothing else announces that it is back.
    /// </summary>
    public async Task PublishAsync(
        CommunityEventModerationChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (domainEvent.CurrentState is CommunityLifeModerationState.Hidden
            or CommunityLifeModerationState.Suspended)
        {
            await PublishArchivedAsync(
                domainEvent.CommunityEventId,
                domainEvent.OrganizationId,
                domainEvent.CurrentState.ToString(),
                isModerationAction: true,
                domainEvent.Reason,
                domainEvent.OccurredAt,
                cancellationToken);

            return;
        }

        if (domainEvent.CurrentState != CommunityLifeModerationState.Clear
            || domainEvent.PreviousState is not (CommunityLifeModerationState.Hidden
                or CommunityLifeModerationState.Suspended))
        {
            return;
        }

        var communityEvent = await _events.GetAsync(
            domainEvent.CommunityEventId, cancellationToken);

        // Only an event that is still live comes back. One cleared after it was cancelled or its
        // date passed would be re-indexed as something members could still respond to.
        if (communityEvent is { Status: EventStatus.Published })
        {
            await PublishLiveAsync(
                communityEvent,
                isFirstPublication: false,
                domainEvent.OccurredAt,
                cancellationToken);
        }
    }

    private async Task PublishArchivedAsync(
        CommunityEventId communityEventId,
        OrganizationId? organizationId,
        string currentState,
        bool isModerationAction,
        string? reason,
        DateTimeOffset archivedAt,
        CancellationToken cancellationToken)
    {
        var communityEvent = await _events.GetAsync(communityEventId, cancellationToken);

        await PublishSafelyAsync(
            new EventArchivedIntegrationEvent
            {
                CommunityEventId = communityEventId.Value,
                OrganizationId = organizationId?.Value,
                Slug = communityEvent?.Slug.Value ?? string.Empty,
                CurrentState = currentState,
                IsModerationAction = isModerationAction,
                Reason = reason,
                ArchivedAt = archivedAt,
                OrganizerUserId = communityEvent?.OrganizerUserId.Value
            },
            cancellationToken);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Attendance
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A response moving, translated into whichever published contract it corresponds to.
    /// <para>
    /// <b>This is the only place an RSVP is published, including a brand-new one.</b> Responding
    /// raises a created event <em>and</em> a status change whose previous status is a synthetic
    /// <c>Cancelled</c>, deliberately, so creation and change travel one code path. Publishing from
    /// the created handler as well would put every first commitment on the bus twice — and consumers
    /// de-duplicate on <c>EventId</c>, which differs between two genuinely distinct events.
    /// </para>
    /// <para>
    /// <b>What gets published is a place taken or a place released</b>, derived from the same
    /// capacity predicate the counters move by. Somebody drifting between interested and waitlisted
    /// is browsing, and a consumer acting on it would notify an organizer about somebody who has
    /// decided nothing. Somebody stepping down from going to interested <em>is</em> published, as a
    /// withdrawal: the place genuinely came back, whatever the member called it.
    /// </para>
    /// <para>
    /// <b>A no-show is a withdrawal with a reason, not a separate contract.</b> It releases the same
    /// place and its consumer obligation is identical; what differs is only why, which the
    /// <c>Reason</c> carries.
    /// </para>
    /// </summary>
    /// <param name="organizerUserId">
    /// The member running the event — B13.1. Passed in rather than read back, for the same reason
    /// the counters are: the handler has already loaded the event to settle attendance against it,
    /// and this path runs on every response the module receives. Null when the event is gone, in
    /// which case nobody is told and that is correct.
    /// </param>
    /// <param name="event">
    /// The event itself, or null when it was gone by the time the handler read it back — B13.3.
    /// <para>
    /// <b>Passed instead of two more primitives, and it is the only place in this relay that takes
    /// an aggregate.</b> The alternative was a title and a nullable start alongside the existing
    /// non-null <paramref name="startsAt"/>, whose fallback is the response's own timestamp — and a
    /// reminder scheduled against "now" fires immediately, which is the failure the field exists to
    /// prevent. Nothing here mutates it and only two values are read off it.
    /// </para>
    /// </param>
    public async Task PublishAsync(
        EventRsvpStatusChangedDomainEvent domainEvent,
        DateTimeOffset startsAt,
        int goingCount,
        int? remainingPlaces,
        Guid? organizerUserId,
        CommunityEvent? @event,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var held = RsvpStatusTransitions.HoldsCapacity(domainEvent.PreviousStatus);
        var holds = RsvpStatusTransitions.HoldsCapacity(domainEvent.CurrentStatus);

        // A first response that takes no place yet — interested, or straight onto a full event's
        // waiting list. Worth publishing because it is the event's first signal from that member,
        // and worth distinguishing from a drift between the two, which is not.
        var isFirstResponse = !held
            && !holds
            && domainEvent.CurrentStatus is RsvpStatus.Interested or RsvpStatus.Waitlisted
            && domainEvent.PreviousStatus is RsvpStatus.Cancelled or RsvpStatus.NoShow;

        if ((holds && !held) || isFirstResponse)
        {
            await PublishSafelyAsync(
                new RsvpSubmittedIntegrationEvent
                {
                    RsvpId = domainEvent.RsvpId.Value,
                    CommunityEventId = domainEvent.CommunityEventId.Value,
                    OrganizationId = domainEvent.OrganizationId?.Value,
                    AttendeeUserId = domainEvent.AttendeeUserId.Value,
                    Status = domainEvent.CurrentStatus.ToString(),
                    PartySize = domainEvent.PartySize,
                    GoingCount = goingCount,
                    RemainingPlaces = remainingPlaces,
                    RespondedAt = domainEvent.ChangedAt,

                    // B13.1. Until this field existed, "somebody is coming" could be confirmed to
                    // the person who said it and could not be told to the person running the hall.
                    OrganizerUserId = organizerUserId,

                    // B13.3. The moment a reminder is scheduled against, and the name it puts on
                    // screen. Both read off the aggregate the handler already loaded, so neither
                    // costs a query. Null when the event was gone — deliberately not falling back to
                    // the response's own timestamp the way the withdrawal computation does, because
                    // a reminder scheduled against "now" fires immediately, which is precisely the
                    // failure this field exists to prevent.
                    EventStartsAt = @event?.Schedule.StartsAt,
                    EventTitle = @event?.Title.Value
                },
                cancellationToken);

            return;
        }

        if (!held || holds)
        {
            return;
        }

        await PublishSafelyAsync(
            new RsvpWithdrawnIntegrationEvent
            {
                RsvpId = domainEvent.RsvpId.Value,
                CommunityEventId = domainEvent.CommunityEventId.Value,
                OrganizationId = domainEvent.OrganizationId?.Value,
                AttendeeUserId = domainEvent.AttendeeUserId.Value,
                PartySize = domainEvent.PartySize,
                Reason = domainEvent.DeclineReason == RsvpDeclineReason.None
                    ? domainEvent.CurrentStatus.ToString()
                    : domainEvent.DeclineReason.ToString(),

                // Negative when somebody withdraws after the doors opened, which is the shape a
                // consumer needs to tell a late cancellation from a no-show reconciled afterwards.
                HoursBeforeStart = (startsAt - domainEvent.ChangedAt).TotalHours,
                GoingCount = goingCount,
                RemainingPlaces = remainingPlaces,
                WithdrawnAt = domainEvent.ChangedAt,
                OrganizerUserId = organizerUserId
            },
            cancellationToken);
    }

    public Task PublishAsync(
        EventCheckInCompletedDomainEvent domainEvent,
        int checkedInCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new EventCheckInCompletedIntegrationEvent
            {
                RsvpId = domainEvent.RsvpId.Value,
                CommunityEventId = domainEvent.CommunityEventId.Value,
                OrganizationId = domainEvent.OrganizationId?.Value,
                AttendeeUserId = domainEvent.AttendeeUserId.Value,
                Method = domainEvent.Method.ToString(),
                RecordedByUserId = domainEvent.RecordedBy?.Value,
                PartySize = domainEvent.PartySize,
                CheckedInCount = checkedInCount,
                CheckedInAt = domainEvent.CheckedInAt
            },
            cancellationToken);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Media
    // ════════════════════════════════════════════════════════════════════════════════════════

    public Task PublishAsync(
        CommunityEventMediaAttachedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        // Verification evidence is never announced. It is a constitution or a registration
        // certificate submitted to Trust, and a subscriber that indexed it would surface it beside
        // a public gallery.
        if (domainEvent.Role == CommunityLifeMediaRole.VerificationEvidence)
        {
            return Task.CompletedTask;
        }

        return PublishSafelyAsync(
            new CommunityLifeMediaAttachedIntegrationEvent
            {
                SubjectType = nameof(ReportSubjectType.Event),
                SubjectId = domainEvent.CommunityEventId.Value,
                OrganizationId = domainEvent.OrganizationId?.Value,
                MediaId = domainEvent.MediaId,
                Role = domainEvent.Role.ToString(),
                AttachedAt = domainEvent.OccurredAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        OrganizationMediaAttachedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (domainEvent.Role == CommunityLifeMediaRole.VerificationEvidence)
        {
            return Task.CompletedTask;
        }

        return PublishSafelyAsync(
            new CommunityLifeMediaAttachedIntegrationEvent
            {
                SubjectType = nameof(ReportSubjectType.Organization),
                SubjectId = domainEvent.OrganizationId.Value,
                OrganizationId = domainEvent.OrganizationId.Value,
                MediaId = domainEvent.MediaId,
                Role = domainEvent.Role.ToString(),
                AttachedAt = domainEvent.OccurredAt
            },
            cancellationToken);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Trust & safety
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A report. Carries no reporter identity, on either contract — a subscriber does not need to
    /// know who complained, and a bus is the wrong place to learn it.
    /// </summary>
    public async Task PublishAsync(
        CommunityLifeReportedDomainEvent domainEvent,
        bool wasFlagged,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (domainEvent.SubjectType == ReportSubjectType.Organization)
        {
            await PublishSafelyAsync(
                new OrganizationReportedIntegrationEvent
                {
                    ReportId = domainEvent.ReportId.Value,
                    OrganizationId = domainEvent.SubjectId,
                    Reason = domainEvent.Reason.ToString(),
                    TotalReports = domainEvent.TotalReports,
                    WasFlagged = wasFlagged,
                    ReportedAt = domainEvent.ReportedAt,

                    // The subject of the report. Still never the reporter.
                    OrganizerUserId = await AdministratorOfAsync(
                        OrganizationId.From(domainEvent.SubjectId), cancellationToken)
                },
                cancellationToken);

            return;
        }

        var communityEvent = await _events.GetAsync(
            CommunityEventId.From(domainEvent.SubjectId), cancellationToken);

        await PublishSafelyAsync(
            new EventReportedIntegrationEvent
            {
                ReportId = domainEvent.ReportId.Value,
                CommunityEventId = domainEvent.SubjectId,
                OrganizationId = communityEvent?.OrganizationId?.Value,
                Reason = domainEvent.Reason.ToString(),
                TotalReports = domainEvent.TotalReports,
                WasFlagged = wasFlagged,
                ReportedAt = domainEvent.ReportedAt,

                // The subject of the report. Still never the reporter.
                OrganizerUserId = communityEvent?.OrganizerUserId.Value
            },
            cancellationToken);
    }

    /// <summary>
    /// The facets a category belongs to, flattened for a consumer that has no catalogue of its own.
    /// <para>
    /// A search index gets the facet names in the payload rather than having to call back for them,
    /// which is what lets a filter bar be built from published events alone.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<string>> FacetsForAsync(
        string categoryKey, CancellationToken cancellationToken)
    {
        var category = await _categories.FindByKeyAsync(categoryKey, cancellationToken);

        return category is null
            ? []
            : [.. category.Facets.Split(
                ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    /// <summary>
    /// A report upheld (REP-13). Who owns the event or organisation is not on the report, so no
    /// owner travels; Trust pays the reporter either way.
    /// </summary>
    public Task PublishAsync(
        CommunityLifeReportUpheldDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new ContentReportUpheldIntegrationEvent
            {
                ReportId = domainEvent.ReportId.Value,
                Module = "community-life",
                ContentType = domainEvent.SubjectType == ReportSubjectType.Organization ? "organization" : "event",
                ContentId = domainEvent.SubjectId,
                ReporterUserId = domainEvent.ReportedByUserId.Value,
                UpheldAt = domainEvent.UpheldAt
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
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A failed publish must not roll back a successful write.
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Publishing {EventType} failed. The write succeeded; the notification did not.",
                typeof(TEvent).Name);
        }
#pragma warning restore CA1031
    }
}
