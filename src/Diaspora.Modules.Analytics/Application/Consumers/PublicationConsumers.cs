using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Contracts.CommunityLife.V1.Events;
using Diaspora.Contracts.Guides.V1.Events;
using Diaspora.Contracts.Housing.V1.Events;
using Diaspora.Contracts.Jobs.V1.Events;
using Diaspora.Contracts.Marketplace.V1.Events;
using Diaspora.Contracts.Providers.V1.Events;
using Diaspora.Modules.Analytics.Application.Ingestion;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Policies;

namespace Diaspora.Modules.Analytics.Application.Consumers;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// PUBLICATION
//
// Eight consumers, one per publishing module, and each does exactly two things: records what
// analytics needs to know about the item — whose it is, what to call it, where it was filed — and
// records that a publication happened.
//
// **Only a first publication counts as a publication.** Every one of these contracts carries
// `IsFirstPublication`, and it exists because a republished edit is not new work. A `content
// published` figure that counted every edit would report a productive week for somebody who fixed
// a typo eight times, and the register's own definition — "a republished edit counts once, on its
// first publication" — is the one served on the metric.
//
// The dimension is updated on *every* publication, because a retitled guide should show its new
// title on a performance row. The fact and the dimension are therefore not the same condition, and
// conflating them is how a title stops updating.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Records a guide's publication.</summary>
public sealed class GuidePublishedAnalyticsConsumer
    : IntegrationEventHandler<GuidePublishedIntegrationEvent>
{
    private readonly AnalyticsDimensionRecorder _dimensions;
    private readonly AnalyticsIngestionService _ingestion;

    public GuidePublishedAnalyticsConsumer(
        AnalyticsDimensionRecorder dimensions, AnalyticsIngestionService ingestion)
    {
        _dimensions = dimensions;
        _ingestion = ingestion;
    }

    protected override async Task HandleAsync(
        GuidePublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await _dimensions.ObserveAsync(
            AnalyticsContentType.Guide,
            integrationEvent.GuideId,
            integrationEvent.AuthorId,
            integrationEvent.Title,
            "Published",
            integrationEvent.CategoryKey,
            integrationEvent.CountryCode,
            communityId: null,
            integrationEvent.PublishedAt,
            cancellationToken).ConfigureAwait(false);

        if (!integrationEvent.IsFirstPublication)
        {
            return;
        }

        await _ingestion.IngestAsync(
            new AnalyticsFact(
                AnalyticsEventTypes.ContentPublished,
                Version: 1,
                SourceModule: "Guides",
                integrationEvent.PublishedAt,
                ActorUserId: integrationEvent.AuthorId,
                ContentType: AnalyticsContentType.Guide,
                ContentId: integrationEvent.GuideId,
                OwnerUserId: integrationEvent.AuthorId,
                CountryCode: integrationEvent.CountryCode,
                Qualifier: integrationEvent.CategoryKey,
                SourceEventId: integrationEvent.EventId),
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Records a community event's publication.</summary>
public sealed class EventPublishedAnalyticsConsumer
    : IntegrationEventHandler<EventPublishedIntegrationEvent>
{
    private readonly AnalyticsDimensionRecorder _dimensions;
    private readonly AnalyticsIngestionService _ingestion;

    public EventPublishedAnalyticsConsumer(
        AnalyticsDimensionRecorder dimensions, AnalyticsIngestionService ingestion)
    {
        _dimensions = dimensions;
        _ingestion = ingestion;
    }

    protected override async Task HandleAsync(
        EventPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await _dimensions.ObserveAsync(
            AnalyticsContentType.Event,
            integrationEvent.CommunityEventId,
            integrationEvent.OrganizerUserId,
            integrationEvent.Title,
            "Published",
            integrationEvent.CategoryKey,
            integrationEvent.CountryCode,
            integrationEvent.OrganizationId,
            integrationEvent.PublishedAt,
            cancellationToken).ConfigureAwait(false);

        if (!integrationEvent.IsFirstPublication)
        {
            return;
        }

        await _ingestion.IngestAsync(
            new AnalyticsFact(
                AnalyticsEventTypes.ContentPublished,
                Version: 1,
                SourceModule: "CommunityLife",
                integrationEvent.PublishedAt,
                ActorUserId: integrationEvent.OrganizerUserId,
                ContentType: AnalyticsContentType.Event,
                ContentId: integrationEvent.CommunityEventId,
                OwnerUserId: integrationEvent.OrganizerUserId,
                CountryCode: integrationEvent.CountryCode,
                CommunityId: integrationEvent.OrganizationId,
                Qualifier: integrationEvent.CategoryKey,
                SourceEventId: integrationEvent.EventId),
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Records an organization's publication.
/// <para>
/// <b><c>OrganizerUserId</c> is nullable on this contract and the row is skipped when it is null.</b>
/// An organization with no named publisher cannot be attributed to a member, and attributing it to
/// the organization identifier would put a figure on a creator overview that belongs to nobody.
/// </para>
/// </summary>
public sealed class OrganizationPublishedAnalyticsConsumer
    : IntegrationEventHandler<OrganizationPublishedIntegrationEvent>
{
    private readonly AnalyticsDimensionRecorder _dimensions;
    private readonly AnalyticsIngestionService _ingestion;

    public OrganizationPublishedAnalyticsConsumer(
        AnalyticsDimensionRecorder dimensions, AnalyticsIngestionService ingestion)
    {
        _dimensions = dimensions;
        _ingestion = ingestion;
    }

    protected override async Task HandleAsync(
        OrganizationPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.OrganizerUserId is null || integrationEvent.OrganizerUserId == Guid.Empty)
        {
            return;
        }

        await _dimensions.ObserveAsync(
            AnalyticsContentType.Organization,
            integrationEvent.OrganizationId,
            integrationEvent.OrganizerUserId.Value,
            integrationEvent.Name,
            "Published",
            integrationEvent.OrganizationTypeKey,
            integrationEvent.CountryCode,
            integrationEvent.OrganizationId,
            integrationEvent.PublishedAt,
            cancellationToken).ConfigureAwait(false);

        if (!integrationEvent.IsFirstPublication)
        {
            return;
        }

        await _ingestion.IngestAsync(
            new AnalyticsFact(
                AnalyticsEventTypes.ContentPublished,
                Version: 1,
                SourceModule: "CommunityLife",
                integrationEvent.PublishedAt,
                ActorUserId: integrationEvent.OrganizerUserId,
                ContentType: AnalyticsContentType.Organization,
                ContentId: integrationEvent.OrganizationId,
                OwnerUserId: integrationEvent.OrganizerUserId,
                CountryCode: integrationEvent.CountryCode,
                CommunityId: integrationEvent.OrganizationId,
                SourceEventId: integrationEvent.EventId),
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Records a marketplace listing's publication.
/// <para>
/// <b><c>SellerId</c> is a seller profile and this module needs a member.</b> B10 keeps a seller
/// profile per member with its own identifier, and the contract carries both: <c>SellerId</c>, the
/// profile, and <c>SellerUserId</c>, the member. This summary used to say the two were the same
/// identifier; they never were, and every seller dashboard stayed empty until 13 Sep 2026 because
/// rows were keyed by the profile. The owner is <c>SellerUserId</c>, and only that.
/// </para>
/// </summary>
public sealed class ListingPublishedAnalyticsConsumer
    : IntegrationEventHandler<ListingPublishedIntegrationEvent>
{
    private readonly AnalyticsDimensionRecorder _dimensions;
    private readonly AnalyticsIngestionService _ingestion;

    public ListingPublishedAnalyticsConsumer(
        AnalyticsDimensionRecorder dimensions, AnalyticsIngestionService ingestion)
    {
        _dimensions = dimensions;
        _ingestion = ingestion;
    }

    protected override async Task HandleAsync(
        ListingPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // The owner is the member, not their seller profile. `SellerId` is the Marketplace module's
        // own identifier and names no user; writing it into a column read as a user id is what left
        // every seller dashboard permanently empty — their rows keyed by an id no query would ever
        // ask for. `Guid.Empty` where the event has no user is the recorder's "unknown", which it
        // will not write over a known owner. Never `?? SellerId`: that is the defect again.
        await _dimensions.ObserveAsync(
            AnalyticsContentType.Marketplace,
            integrationEvent.ListingId,
            integrationEvent.SellerUserId ?? Guid.Empty,
            integrationEvent.Title,
            "Available",
            integrationEvent.CategoryKey,
            integrationEvent.CountryCode,
            communityId: null,
            integrationEvent.PublishedAt,
            cancellationToken).ConfigureAwait(false);

        if (!integrationEvent.IsFirstPublication)
        {
            return;
        }

        await _ingestion.IngestAsync(
            new AnalyticsFact(
                AnalyticsEventTypes.ContentPublished,
                Version: 1,
                SourceModule: "Marketplace",
                integrationEvent.PublishedAt,
                ActorUserId: integrationEvent.SellerUserId,
                ContentType: AnalyticsContentType.Marketplace,
                ContentId: integrationEvent.ListingId,
                OwnerUserId: integrationEvent.SellerUserId,
                CountryCode: integrationEvent.CountryCode,
                Qualifier: integrationEvent.CategoryKey,
                SourceEventId: integrationEvent.EventId),
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Records a housing listing's publication.</summary>
public sealed class HousingPublishedAnalyticsConsumer
    : IntegrationEventHandler<HousingPublishedIntegrationEvent>
{
    private readonly AnalyticsDimensionRecorder _dimensions;
    private readonly AnalyticsIngestionService _ingestion;

    public HousingPublishedAnalyticsConsumer(
        AnalyticsDimensionRecorder dimensions, AnalyticsIngestionService ingestion)
    {
        _dimensions = dimensions;
        _ingestion = ingestion;
    }

    protected override async Task HandleAsync(
        HousingPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // The owner is the member, not their landlord profile. `LandlordId` is the Housing module's
        // own identifier and names no user; writing it into a column read as a user id is what left
        // every landlord dashboard permanently empty — their rows keyed by an id no query would ever
        // ask for. `Guid.Empty` where the event has no user is the recorder's "unknown", which it
        // will not write over a known owner. Never `?? LandlordId`: that is the defect again.
        await _dimensions.ObserveAsync(
            AnalyticsContentType.Housing,
            integrationEvent.ListingId,
            integrationEvent.LandlordUserId ?? Guid.Empty,
            integrationEvent.Title,
            "Available",
            integrationEvent.AccommodationTypeKey,
            integrationEvent.CountryCode,
            communityId: null,
            integrationEvent.PublishedAt,
            cancellationToken).ConfigureAwait(false);

        if (!integrationEvent.IsFirstPublication)
        {
            return;
        }

        await _ingestion.IngestAsync(
            new AnalyticsFact(
                AnalyticsEventTypes.ContentPublished,
                Version: 1,
                SourceModule: "Housing",
                integrationEvent.PublishedAt,
                ActorUserId: integrationEvent.LandlordUserId,
                ContentType: AnalyticsContentType.Housing,
                ContentId: integrationEvent.ListingId,
                OwnerUserId: integrationEvent.LandlordUserId,
                CountryCode: integrationEvent.CountryCode,
                Qualifier: integrationEvent.AccommodationTypeKey,
                SourceEventId: integrationEvent.EventId),
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Records a job posting's publication.</summary>
public sealed class JobPublishedAnalyticsConsumer
    : IntegrationEventHandler<JobPublishedIntegrationEvent>
{
    private readonly AnalyticsDimensionRecorder _dimensions;
    private readonly AnalyticsIngestionService _ingestion;

    public JobPublishedAnalyticsConsumer(
        AnalyticsDimensionRecorder dimensions, AnalyticsIngestionService ingestion)
    {
        _dimensions = dimensions;
        _ingestion = ingestion;
    }

    protected override async Task HandleAsync(
        JobPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // The owner is the member, not their employer profile. `EmployerId` is the Jobs module's
        // own identifier and names no user; writing it into a column read as a user id is what left
        // every employer dashboard permanently empty — their rows keyed by an id no query would ever
        // ask for. `Guid.Empty` where the event has no user is the recorder's "unknown", which it
        // will not write over a known owner. Never `?? EmployerId`: that is the defect again.
        await _dimensions.ObserveAsync(
            AnalyticsContentType.Job,
            integrationEvent.JobId,
            integrationEvent.EmployerUserId ?? Guid.Empty,
            integrationEvent.Title,
            "Open",
            integrationEvent.CategoryKey,
            integrationEvent.CountryCode,
            communityId: null,
            integrationEvent.PublishedAt,
            cancellationToken).ConfigureAwait(false);

        if (!integrationEvent.IsFirstPublication)
        {
            return;
        }

        await _ingestion.IngestAsync(
            new AnalyticsFact(
                AnalyticsEventTypes.ContentPublished,
                Version: 1,
                SourceModule: "Jobs",
                integrationEvent.PublishedAt,
                ActorUserId: integrationEvent.EmployerUserId,
                ContentType: AnalyticsContentType.Job,
                ContentId: integrationEvent.JobId,
                OwnerUserId: integrationEvent.EmployerUserId,
                CountryCode: integrationEvent.CountryCode,
                Qualifier: integrationEvent.CategoryKey,
                SourceEventId: integrationEvent.EventId),
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Records a community post's publication.
/// <para>
/// The client calls this content kind an Update; B6 calls it a Post. The vocabulary translation
/// lives in <c>AnalyticsContentTypes</c> and not here.
/// </para>
/// </summary>
public sealed class CommunityPostPublishedAnalyticsConsumer
    : IntegrationEventHandler<CommunityPostPublishedIntegrationEvent>
{
    private readonly AnalyticsDimensionRecorder _dimensions;
    private readonly AnalyticsIngestionService _ingestion;

    public CommunityPostPublishedAnalyticsConsumer(
        AnalyticsDimensionRecorder dimensions, AnalyticsIngestionService ingestion)
    {
        _dimensions = dimensions;
        _ingestion = ingestion;
    }

    protected override async Task HandleAsync(
        CommunityPostPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await _dimensions.ObserveAsync(
            AnalyticsContentType.Post,
            integrationEvent.PostId,
            integrationEvent.AuthorId,
            title: null,
            "Published",
            integrationEvent.Category,
            integrationEvent.CountryCode,
            integrationEvent.OrganizationId,
            integrationEvent.PublishedAt,
            cancellationToken).ConfigureAwait(false);

        await _ingestion.IngestAsync(
            new AnalyticsFact(
                AnalyticsEventTypes.ContentPublished,
                Version: 1,
                SourceModule: "Community",
                integrationEvent.PublishedAt,
                ActorUserId: integrationEvent.AuthorId,
                ContentType: AnalyticsContentType.Post,
                ContentId: integrationEvent.PostId,
                OwnerUserId: integrationEvent.AuthorId,
                CountryCode: integrationEvent.CountryCode,
                CommunityId: integrationEvent.OrganizationId,
                Qualifier: integrationEvent.Category,
                SourceEventId: integrationEvent.EventId),
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Records a service provider profile's publication.</summary>
public sealed class ProviderPublishedAnalyticsConsumer
    : IntegrationEventHandler<ProviderPublishedIntegrationEvent>
{
    private readonly AnalyticsDimensionRecorder _dimensions;
    private readonly AnalyticsIngestionService _ingestion;

    public ProviderPublishedAnalyticsConsumer(
        AnalyticsDimensionRecorder dimensions, AnalyticsIngestionService ingestion)
    {
        _dimensions = dimensions;
        _ingestion = ingestion;
    }

    protected override async Task HandleAsync(
        ProviderPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await _dimensions.ObserveAsync(
            AnalyticsContentType.Provider,
            integrationEvent.ProviderId,
            integrationEvent.OwnerId,
            integrationEvent.Name,
            "Open",
            integrationEvent.CategoryKey,
            integrationEvent.CountryCode,
            communityId: null,
            integrationEvent.PublishedAt,
            cancellationToken).ConfigureAwait(false);

        await _ingestion.IngestAsync(
            new AnalyticsFact(
                AnalyticsEventTypes.ContentPublished,
                Version: 1,
                SourceModule: "Providers",
                integrationEvent.PublishedAt,
                ActorUserId: integrationEvent.OwnerId,
                ContentType: AnalyticsContentType.Provider,
                ContentId: integrationEvent.ProviderId,
                OwnerUserId: integrationEvent.OwnerId,
                CountryCode: integrationEvent.CountryCode,
                Qualifier: integrationEvent.CategoryKey,
                SourceEventId: integrationEvent.EventId),
            cancellationToken).ConfigureAwait(false);
    }
}
