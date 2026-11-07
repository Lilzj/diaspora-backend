using Diaspora.Contracts.Content.V1.Events;
using Diaspora.Contracts.Events;
using Diaspora.Contracts.Housing.V1.Events;
using Diaspora.Contracts.Moderation.V1;
using Diaspora.Contracts.Moderation.V1.Events;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Events;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Neighborhoods;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.Modules.Housing.Domain.Reports;
using Diaspora.Platform.Messaging;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Housing.Application.EventHandlers;

/// <summary>
/// Turns B11's domain events into the published integration events other modules consume.
/// <para>
/// <b>The one translation seam, and the reason B11's domain stays private.</b> A domain event
/// carries strongly typed identifiers and domain enums; an integration event carries flat
/// primitives, so a consumer can parse it without referencing this module. Doing the translation in
/// a single class means the two never drift, and means a consumer's contract cannot be changed by a
/// refactor inside an aggregate.
/// </para>
/// <para>
/// <b>A publish failure never fails the write.</b> The aggregate is already saved by the time this
/// runs; throwing here would roll back a completed letting because a notification did not go out.
/// Failures are logged, and at-least-once delivery is the contract — consumers treat
/// <c>EventId</c> as the idempotency key.
/// </para>
/// <para>
/// <b>Three domain events are deliberately never published.</b> A rejected insight says what a
/// member tried to say about a place and is between them and the moderator; an endorsement is the
/// most behaviourally revealing thing this module knows about a member and is of no use to any
/// other; and a viewing <em>request</em> is somebody's intention to visit a named address, which is
/// not a fact that belongs on a bus. The scheduling that follows is published, because by then both
/// parties have agreed to it.
/// </para>
/// </summary>
public sealed class HousingIntegrationEventRelay
{
    private readonly IIntegrationEventPublisher _publisher;
    private readonly IHousingListingRepository _listings;
    private readonly ILandlordProfileRepository _landlords;
    private readonly INeighborhoodProfileRepository _profiles;
    private readonly ILogger<HousingIntegrationEventRelay> _logger;

    public HousingIntegrationEventRelay(
        IIntegrationEventPublisher publisher,
        IHousingListingRepository listings,
        ILandlordProfileRepository landlords,
        INeighborhoodProfileRepository profiles,
        ILogger<HousingIntegrationEventRelay> logger)
    {
        _publisher = publisher;
        _listings = listings;
        _landlords = landlords;
        _profiles = profiles;
        _logger = logger;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Listing lifecycle
    // ════════════════════════════════════════════════════════════════════════════════════════

    public Task PublishAsync(
        HousingListingDeletedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new HousingDeletedIntegrationEvent
            {
                ListingId = domainEvent.ListingId.Value,
                LandlordId = domainEvent.LandlordId.Value,
                DeletedAt = domainEvent.DeletedAt
            },
            cancellationToken);
    }

    public Task PublishAsync(
        HousingListingCreatedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new HousingCreatedIntegrationEvent
            {
                ListingId = domainEvent.ListingId.Value,
                LandlordId = domainEvent.LandlordId.Value,
                CreatedBy = domainEvent.CreatedBy.Value,
                Title = domainEvent.Title,
                Slug = domainEvent.Slug,
                AccommodationTypeKey = domainEvent.AccommodationTypeKey,
                CountryCode = domainEvent.CountryCode,
                NeighborhoodAreaId = domainEvent.NeighborhoodAreaId,
                CreatedAt = domainEvent.OccurredAt
            },
            cancellationToken);
    }

    /// <summary>
    /// The module's most consequential contract. Reads the listing and landlord back so the payload
    /// carries everything a card-level index needs.
    /// <para>
    /// The read is inline, inside the same unit of work that raised the event, so it is the identity
    /// map rather than a round trip.
    /// </para>
    /// </summary>
    public async Task PublishAsync(
        HousingListingPublishedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var listing = await _listings.GetAsync(domainEvent.ListingId, cancellationToken);
        var landlord = await _landlords.GetAsync(domainEvent.LandlordId, cancellationToken);

        await PublishSafelyAsync(
            new HousingPublishedIntegrationEvent
            {
                ListingId = domainEvent.ListingId.Value,
                LandlordId = domainEvent.LandlordId.Value,
                LandlordName = landlord?.Identity.DisplayName ?? string.Empty,
                LandlordKind = (landlord?.Identity.Kind ?? LandlordKind.Individual).ToString(),
                IsLandlordVerified = landlord?.IsVerified ?? false,
                Title = domainEvent.Title,
                Slug = domainEvent.Slug,
                Summary = listing?.Description.Summary,
                AccommodationTypeKey = domainEvent.AccommodationTypeKey,
                IsRentDisclosed = listing?.Price.IsDisclosed ?? false,
                RentAmount = listing?.Price.Amount,
                CurrencyCode = listing?.Price.CurrencyCode,
                RentPeriod = listing?.Price.Period == RentPeriod.None
                    ? null
                    : listing?.Price.Period.ToString(),
                DepositAmount = listing?.Price.DepositAmount,
                Bedrooms = listing?.Capacity.Bedrooms ?? 0,
                Bathrooms = listing?.Capacity.Bathrooms ?? 0m,
                FurnishedStatus =
                    (listing?.FurnishedStatus ?? FurnishedStatus.Unfurnished).ToString(),
                TotalUnits = listing?.Availability.TotalUnits ?? 1,
                AvailableFrom = listing?.Availability.AvailableFrom,
                AcceptsShortStay = listing?.StayPolicy.AcceptsShortStay ?? false,
                AcceptsLongStay = listing?.StayPolicy.AcceptsLongStay ?? true,
                IsStudentFriendly = listing?.StayPolicy.IsStudentFriendly ?? false,
                IsFamilyFriendly = listing?.StayPolicy.IsFamilyFriendly ?? false,
                IsSharedAccommodation = listing?.StayPolicy.IsSharedAccommodation ?? false,
                CountryCode = domainEvent.CountryCode,
                RegionName = listing?.Location.RegionName,
                CityName = listing?.Location.CityName,
                NeighborhoodName = listing?.Location.NeighborhoodName,
                NeighborhoodAreaId = domainEvent.NeighborhoodAreaId,
                CoverMediaId = listing?.CoverImage?.MediaId,
                AmenityKeys = listing is null
                    ? []
                    : [.. listing.Amenities.Select(amenity => amenity.AmenityKey)],
                IsFirstPublication = domainEvent.IsFirstPublication,
                PublishedAt = domainEvent.PublishedAt,

                // The member behind the profile. Already loaded above for the name, the kind and the
                // badge, so this costs nothing, and without it a consumer that credits the person who
                // listed the property has a profile identifier and no way to reach them.
                LandlordUserId = landlord?.OwnerUserId.Value
            },
            cancellationToken);
    }

    public async Task PublishAsync(
        HousingListingOccupiedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var listing = await _listings.GetAsync(domainEvent.ListingId, cancellationToken);

        await PublishSafelyAsync(
            new HousingOccupiedIntegrationEvent
            {
                ListingId = domainEvent.ListingId.Value,
                LandlordId = domainEvent.LandlordId.Value,
                OccupantUserId = domainEvent.OccupantUserId?.Value,
                ReservationId = domainEvent.ReservationId?.Value,
                Units = domainEvent.Units,
                IsFullyOccupied = domainEvent.IsFullyOccupied,
                LetAtAmount = listing?.Price.Amount,
                CurrencyCode = listing?.Price.CurrencyCode,
                RentPeriod = listing?.Price.Period == RentPeriod.None
                    ? null
                    : listing?.Price.Period.ToString(),
                AccommodationTypeKey = listing?.AccommodationTypeKey ?? string.Empty,
                CountryCode = listing?.Location.CountryCode ?? string.Empty,
                NeighborhoodAreaId = listing?.Location.NeighborhoodAreaId,
                TimeToLetDays = domainEvent.TimeToLet?.TotalDays,
                OccupiedAt = domainEvent.OccupiedAt
            },
            cancellationToken);
    }

    /// <summary>
    /// A landlord changed the property's content — B23.x Wave 12.
    /// <para>
    /// <b>The domain event has been raised since the first release and was never published.</b> Its
    /// own summary says the changed-field list is what makes it useful rather than noisy, and until
    /// now nothing outside the module could read either the list or the new rent. Marketplace has
    /// published the equivalent since B10, which is why a member could watch the price of a sofa and
    /// not the rent on a flat.
    /// </para>
    /// <para>
    /// The listing is read back so the payload carries the rent as it now stands — the same inline
    /// read the publication contract does, inside the same unit of work that raised the event.
    /// </para>
    /// </summary>
    public async Task PublishAsync(
        HousingListingUpdatedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var carriesRent = domainEvent.ChangedFields.Contains("Price", StringComparer.OrdinalIgnoreCase);
        var listing = await _listings.GetAsync(domainEvent.ListingId, cancellationToken);

        await PublishSafelyAsync(
            new HousingUpdatedIntegrationEvent
            {
                ListingId = domainEvent.ListingId.Value,
                LandlordId = domainEvent.LandlordId.Value,
                ChangedBy = domainEvent.ChangedBy?.Value,
                ChangedFields = domainEvent.ChangedFields,

                // Only where the rent is what moved, and only where the landlord disclosed it. A
                // figure on every edit would make a price-watch consumer fire on a typo, and a figure
                // the landlord withheld would be the platform publishing what they chose not to.
                IsRentDisclosed = listing?.Price.IsDisclosed ?? false,
                RentAmount = carriesRent && listing?.Price.IsDisclosed == true
                    ? listing.Price.Amount
                    : null,

                // The previous rent is not knowable here: the aggregate already holds the new one by
                // the time this runs, and B11 keeps no price history. Null rather than a guess —
                // Marketplace's equivalent handler makes the same call for the same reason.
                PreviousRentAmount = null,
                CurrencyCode = carriesRent ? listing?.Price.CurrencyCode : null,
                RentPeriod = carriesRent && listing?.Price.Period is { } period
                             && period != RentPeriod.None
                    ? period.ToString()
                    : null,
                CountryCode = listing?.Location.CountryCode,
                NeighborhoodAreaId = listing?.Location.NeighborhoodAreaId,
                ChangedAt = domainEvent.OccurredAt,

                // 21 Oct 2026. A draft's rent change reached strangers' price-watch automations.
                // Who may be told is this module's rule; a listing that could not be read back is
                // told to nobody.
                IsListedPublicly = listing?.IsDiscoverable ?? false,
                AccommodationTypeKey = listing?.AccommodationTypeKey,
                Bedrooms = listing?.Capacity.Bedrooms,
                FurnishedStatus = listing?.FurnishedStatus.ToString(),
                AcceptsShortStay = listing?.StayPolicy.AcceptsShortStay ?? false,
                IsStudentFriendly = listing?.StayPolicy.IsStudentFriendly ?? false
            },
            cancellationToken);
    }

    public Task PublishAsync(
        HousingMediaAttachedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new HousingMediaAttachedIntegrationEvent
            {
                ListingId = domainEvent.ListingId.Value,
                LandlordId = domainEvent.LandlordId.Value,
                MediaId = domainEvent.MediaId,
                Role = domainEvent.Role.ToString(),
                AttachedAt = domainEvent.OccurredAt
            },
            cancellationToken);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Relocation workflow
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A viewing moving on, translated into whichever published contract it corresponds to.
    /// <para>
    /// <b>This is why the aggregate raises one status-changed event rather than five.</b> A
    /// <em>request</em> is deliberately not published at all — somebody's intention to visit a named
    /// address is not a fact that belongs on a bus. Scheduling is, because by then both parties have
    /// agreed to it.
    /// </para>
    /// </summary>
    public Task PublishAsync(
        HousingViewingStatusChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (domainEvent.CurrentStatus == HousingViewingStatus.Scheduled)
        {
            return PublishSafelyAsync(
                new ViewingScheduledIntegrationEvent
                {
                    ViewingId = domainEvent.ViewingId.Value,
                    ListingId = domainEvent.ListingId.Value,
                    LandlordId = domainEvent.LandlordId.Value,
                    SeekerUserId = domainEvent.SeekerUserId.Value,
                    ScheduledFor = domainEvent.ScheduledFor ?? domainEvent.ChangedAt,
                    DurationMinutes = 0,
                    IsVirtual = false,
                    ResponseHours = null,
                    ScheduledAt = domainEvent.ChangedAt
                },
                cancellationToken);
        }

        if (domainEvent.CurrentStatus is HousingViewingStatus.Requested
            or HousingViewingStatus.Scheduled)
        {
            return Task.CompletedTask;
        }

        return PublishSafelyAsync(
            new ViewingCompletedIntegrationEvent
            {
                ViewingId = domainEvent.ViewingId.Value,
                ListingId = domainEvent.ListingId.Value,
                LandlordId = domainEvent.LandlordId.Value,
                SeekerUserId = domainEvent.SeekerUserId.Value,
                Outcome = domainEvent.CurrentStatus.ToString(),
                WasNoShow = domainEvent.CurrentStatus == HousingViewingStatus.NoShow,
                LandlordFailedToAttend = null,
                CompletedAt = domainEvent.ChangedAt
            },
            cancellationToken);
    }

    public async Task PublishAsync(
        HousingReservationCreatedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var listing = await _listings.GetAsync(domainEvent.ListingId, cancellationToken);

        await PublishSafelyAsync(
            new HousingReservedIntegrationEvent
            {
                ReservationId = domainEvent.ReservationId.Value,
                ListingId = domainEvent.ListingId.Value,
                LandlordId = domainEvent.LandlordId.Value,
                SeekerUserId = domainEvent.SeekerUserId.Value,
                Units = domainEvent.Units,
                RemainingUnits = listing?.Availability.AvailableUnits ?? 0,
                MoveInDate = domainEvent.MoveInDate,
                HoldExpiresAt = domainEvent.HoldExpiresAt,
                ReservedAt = domainEvent.ReservedAt
            },
            cancellationToken);
    }

    /// <summary>
    /// A reservation moving, and — separately — a reservation ending without anybody moving in.
    /// <para>
    /// Two contracts from one domain event, for two different audiences. The seeker's contract carries
    /// every transition; the cancellation contract carries the three that return units, and occupation
    /// is deliberately not among them — the listing's own occupied event reports it, and publishing
    /// both would count one letting twice in every consumer's metrics.
    /// </para>
    /// </summary>
    public async Task PublishAsync(
        HousingReservationStatusChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        // ── Every transition, for the seeker — B23.x Wave 12 ──────────────────────────────────
        //
        // Published first and unconditionally, and it is a second contract rather than a widening of
        // the cancellation one below. The endings are what a search index and an availability watcher
        // need; the seeker needs the whole workflow, because the transition they are waiting for is
        // the landlord agreeing. Widening `ReservationCancelled` to carry a forward move would make
        // every existing consumer count a letting that had not ended.
        var terminal = domainEvent.CurrentStatus is HousingReservationStatus.Occupied
            or HousingReservationStatus.Cancelled
            or HousingReservationStatus.Declined
            or HousingReservationStatus.Expired;

        await PublishSafelyAsync(
            new HousingApplicationStatusChangedIntegrationEvent
            {
                ReservationId = domainEvent.ReservationId.Value,
                ListingId = domainEvent.ListingId.Value,
                LandlordId = domainEvent.LandlordId.Value,
                SeekerUserId = domainEvent.SeekerUserId.Value,
                PreviousStatus = domainEvent.PreviousStatus.ToString(),
                CurrentStatus = domainEvent.CurrentStatus.ToString(),
                IsTerminal = terminal,
                IsSuccessful = domainEvent.CurrentStatus == HousingReservationStatus.Occupied,
                ChangedBy = domainEvent.ChangedBy?.Value,
                ChangedAt = domainEvent.ChangedAt
            },
            cancellationToken);

        if (domainEvent.CurrentStatus is not (HousingReservationStatus.Cancelled
            or HousingReservationStatus.Declined
            or HousingReservationStatus.Expired))
        {
            return;
        }

        var listing = await _listings.GetAsync(domainEvent.ListingId, cancellationToken);

        await PublishSafelyAsync(
            new ReservationCancelledIntegrationEvent
            {
                ReservationId = domainEvent.ReservationId.Value,
                ListingId = domainEvent.ListingId.Value,
                LandlordId = domainEvent.LandlordId.Value,
                SeekerUserId = domainEvent.SeekerUserId.Value,
                Units = domainEvent.Units,
                RemainingUnits = listing?.Availability.AvailableUnits ?? 0,
                Outcome = domainEvent.CurrentStatus.ToString(),
                CancelledBy = domainEvent.ChangedBy?.Value,
                Reason = domainEvent.Reason,
                CancelledAt = domainEvent.ChangedAt
            },
            cancellationToken);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Trust & safety
    // ════════════════════════════════════════════════════════════════════════════════════════

    public async Task PublishAsync(
        HousingVerificationChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var landlord = await _landlords.GetAsync(domainEvent.LandlordId, cancellationToken);

        await PublishSafelyAsync(
            new LandlordVerifiedIntegrationEvent
            {
                LandlordId = domainEvent.LandlordId.Value,
                OwnerUserId = landlord?.OwnerUserId.Value ?? Guid.Empty,
                LandlordName = landlord?.Identity.DisplayName ?? string.Empty,
                LandlordKind = (landlord?.Identity.Kind ?? LandlordKind.Individual).ToString(),
                ListingId = domainEvent.ListingId?.Value,
                PreviousState = domainEvent.PreviousState.ToString(),
                CurrentState = domainEvent.CurrentState.ToString(),
                IsVerified =
                    domainEvent.CurrentState == HousingVerificationState.Verified,
                VerificationId = domainEvent.VerificationId,
                Badge = domainEvent.Badge,
                Reason = domainEvent.Reason,
                ChangedAt = domainEvent.ChangedAt
            },
            cancellationToken);
    }

    /// <summary>
    /// A report. Carries no reporter identity — see the contract, and the reason it matters.
    /// </summary>
    public Task PublishAsync(
        HousingReportedDomainEvent domainEvent,
        bool wasFlagged,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new HousingReportedIntegrationEvent
            {
                ReportId = domainEvent.ReportId.Value,
                ListingId = domainEvent.ListingId.Value,
                LandlordId = domainEvent.LandlordId.Value,
                Reason = domainEvent.Reason.ToString(),
                TotalReports = domainEvent.TotalReports,
                WasFlagged = wasFlagged,
                ReportedAt = domainEvent.ReportedAt
            },
            cancellationToken);
    }

    /// <summary>
    /// The same report, sent to Trust's moderation cases (7 Nov 2026), where staff decide it.
    /// <para>
    /// <b>The one published contract that names the reporter, and it is read by Trust alone</b>, which
    /// needs it to count each member once and to tell them the outcome. The landlord is never given
    /// it. The listing and landlord are read back from the identity map for the owner and the title;
    /// a listing that is gone by now has no case to open.
    /// </para>
    /// </summary>
    public async Task PublishReportFiledAsync(
        HousingReportedDomainEvent domainEvent,
        HousingReport report,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        ArgumentNullException.ThrowIfNull(report);

        var listing = await _listings.GetAsync(domainEvent.ListingId, cancellationToken);

        if (listing is null)
        {
            return;
        }

        var landlord = await _landlords.GetAsync(domainEvent.LandlordId, cancellationToken);

        await PublishSafelyAsync(
            new ContentReportFiledIntegrationEvent
            {
                Module = "housing",
                ContentType = ModerationContentTypes.HousingListing,
                ContentId = domainEvent.ListingId.Value,
                ContentOwnerUserId = landlord?.OwnerUserId.Value ?? listing.CreatedByUserId.Value,
                ReporterUserId = report.ReportedBy.Value,
                ReasonCode = HousingModerationReasons.Map(report.Reason).ToString(),
                ModuleReason = report.Reason.ToString(),
                Detail = report.Detail,
                ModuleReportId = report.Id.Value,
                ContentTitle = listing.Title.Value,
                FiledAt = domainEvent.ReportedAt
            },
            cancellationToken);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Neighbourhoods
    // ════════════════════════════════════════════════════════════════════════════════════════

    public Task PublishAsync(
        NeighborhoodInsightSubmittedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        // No body: the text has not been reviewed yet. See the contract.
        return PublishSafelyAsync(
            new NeighborhoodInsightSubmittedIntegrationEvent
            {
                InsightId = domainEvent.InsightId.Value,
                AreaId = domainEvent.AreaId,
                Latitude = domainEvent.Latitude,
                Longitude = domainEvent.Longitude,
                AuthorId = domainEvent.AuthorId.Value,
                Category = domainEvent.Category.ToString(),
                SubmittedAt = domainEvent.SubmittedAt
            },
            cancellationToken);
    }

    /// <summary>
    /// An approved insight, with its text.
    /// <para>
    /// Rejections are deliberately not published. A refused insight's existence is between its
    /// author and the moderator, and broadcasting it would tell every subscriber what a member
    /// tried to say about somewhere.
    /// </para>
    /// </summary>
    public Task PublishAsync(
        NeighborhoodInsightDecidedDomainEvent domainEvent,
        string body,
        string? label,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (domainEvent.CurrentStatus != NeighborhoodInsightStatus.Approved)
        {
            return Task.CompletedTask;
        }

        return PublishSafelyAsync(
            new NeighborhoodInsightApprovedIntegrationEvent
            {
                InsightId = domainEvent.InsightId.Value,
                AreaId = domainEvent.AreaId,
                Latitude = domainEvent.Latitude,
                Longitude = domainEvent.Longitude,
                AuthorId = domainEvent.AuthorId.Value,
                Category = domainEvent.Category.ToString(),
                Body = body,
                Label = label,
                ApprovedBy = domainEvent.DecidedBy?.Value ?? Guid.Empty,
                ApprovedAt = domainEvent.DecidedAt
            },
            cancellationToken);
    }

    public async Task PublishAsync(
        NeighborhoodSummaryUpdatedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var profile = await _profiles.GetByAreaAsync(domainEvent.AreaId, cancellationToken);

        await PublishSafelyAsync(
            new NeighborhoodSummaryUpdatedIntegrationEvent
            {
                AreaId = domainEvent.AreaId,
                Name = profile?.Name ?? string.Empty,
                Slug = profile?.Slug ?? string.Empty,
                CountryCode = profile?.CountryCode ?? string.Empty,
                ChangedFields = domainEvent.ChangedFields,
                IsApproved = domainEvent.IsApproved,
                ChangedBy = domainEvent.ChangedBy?.Value,
                ChangedAt = domainEvent.OccurredAt
            },
            cancellationToken);
    }

    /// <summary>A report upheld (REP-13), with the landlord's owner when the profile was found.</summary>
    public Task PublishAsync(
        HousingReportUpheldDomainEvent domainEvent,
        Guid? ownerUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return PublishSafelyAsync(
            new ContentReportUpheldIntegrationEvent
            {
                ReportId = domainEvent.ReportId.Value,
                Module = "housing",
                ContentType = "listing",
                ContentId = domainEvent.ListingId.Value,
                ReporterUserId = domainEvent.ReportedBy.Value,
                ContentOwnerUserId = ownerUserId,
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
