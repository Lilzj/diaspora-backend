using Diaspora.Application.Abstractions;
using Diaspora.Contracts.CommunityLife.V1.Events;
using Diaspora.Contracts.Content.V1.Events;
using Diaspora.Contracts.CommunityProgramme.V1.Events;
using Diaspora.Contracts.Housing.V1.Events;
using Diaspora.Contracts.Jobs.V1.Events;
using Diaspora.Contracts.Marketplace.V1.Events;
using Diaspora.Contracts.Providers.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Domain;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Trust.Application.Consumers;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// XP FOR WHAT THE REST OF THE PLATFORM PUBLISHES (REP-14, D-3)
//
// Guides, posts and comments paid XP; an event, a service, a job, a home or a listing paid nothing,
// though each module already announced them. These consumers pay the member each event names, on
// today's scale (an event 10, a listing 5), once per entity by its award key. The source is the
// entity, so a removal that names it (a cancelled event, a deleted home or listing) takes the XP back
// through ReputationReversalConsumers. None of them pays reward points: publishing earns XP alone
// (D-2).
//
// Who is paid is the event's own answer, the same one Creator's activity consumers take: the
// organizer, the owner, the employer, the landlord or the seller as a member. Where an event names a
// business rather than a member, it is skipped rather than paid to nobody.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Scores one publication for one member and saves. Shared by the consumers below.</summary>
public sealed class ActivityReputationScorer
{
    private readonly ReputationService _reputation;
    private readonly TrustProfileProvisioner _provisioner;
    private readonly IUnitOfWork _unitOfWork;

    public ActivityReputationScorer(
        ReputationService reputation, TrustProfileProvisioner provisioner, IUnitOfWork unitOfWork)
    {
        _reputation = reputation;
        _provisioner = provisioner;
        _unitOfWork = unitOfWork;
    }

    public async Task ScoreAsync(
        Guid memberUserId,
        string actionType,
        ReputationAwardKey key,
        string sourceType,
        Guid sourceId,
        string? countryCode,
        CancellationToken cancellationToken)
    {
        if (memberUserId == Guid.Empty)
        {
            return;
        }

        var member = UserId.From(memberUserId);

        await _provisioner.GetOrCreateBothAsync(member, cancellationToken);

        await _reputation.ScoreAsync(
            member, actionType, key, sourceType, sourceId.ToString(), countryCode, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Pays the organizer 10 XP for publishing a community event.</summary>
public sealed class EventPublishedReputationConsumer : IntegrationEventHandler<EventPublishedIntegrationEvent>
{
    private readonly ActivityReputationScorer _scorer;

    public EventPublishedReputationConsumer(ActivityReputationScorer scorer) => _scorer = scorer;

    protected override Task HandleAsync(EventPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return _scorer.ScoreAsync(
            integrationEvent.OrganizerUserId,
            ReputationActions.EventPublished,
            ReputationAwardKey.Published(ReputationActions.EventPublished, "event", integrationEvent.CommunityEventId),
            "event",
            integrationEvent.CommunityEventId,
            integrationEvent.CountryCode,
            cancellationToken);
    }
}

/// <summary>Pays the owner 10 XP for publishing a service, once per service.</summary>
public sealed class ProviderPublishedReputationConsumer : IntegrationEventHandler<ProviderPublishedIntegrationEvent>
{
    private readonly ActivityReputationScorer _scorer;

    public ProviderPublishedReputationConsumer(ActivityReputationScorer scorer) => _scorer = scorer;

    protected override Task HandleAsync(ProviderPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // Republishing a service is not a new one: the key is the provider.
        return _scorer.ScoreAsync(
            integrationEvent.OwnerId,
            ReputationActions.ProviderPublished,
            ReputationAwardKey.Published(ReputationActions.ProviderPublished, "provider", integrationEvent.ProviderId),
            "provider",
            integrationEvent.ProviderId,
            integrationEvent.CountryCode,
            cancellationToken);
    }
}

/// <summary>Pays the employer, as a member, 5 XP for posting a job.</summary>
public sealed class JobPublishedReputationConsumer : IntegrationEventHandler<JobPublishedIntegrationEvent>
{
    private readonly ActivityReputationScorer _scorer;

    public JobPublishedReputationConsumer(ActivityReputationScorer scorer) => _scorer = scorer;

    protected override Task HandleAsync(JobPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return integrationEvent.EmployerUserId is { } employer
            ? _scorer.ScoreAsync(
                employer,
                ReputationActions.JobPublished,
                ReputationAwardKey.Published(ReputationActions.JobPublished, "job", integrationEvent.JobId),
                "job",
                integrationEvent.JobId,
                integrationEvent.CountryCode,
                cancellationToken)
            : Task.CompletedTask;
    }
}

/// <summary>Pays the landlord, as a member, 5 XP for listing a home.</summary>
public sealed class HousingPublishedReputationConsumer : IntegrationEventHandler<HousingPublishedIntegrationEvent>
{
    private readonly ActivityReputationScorer _scorer;

    public HousingPublishedReputationConsumer(ActivityReputationScorer scorer) => _scorer = scorer;

    protected override Task HandleAsync(HousingPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return integrationEvent.LandlordUserId is { } landlord
            ? _scorer.ScoreAsync(
                landlord,
                ReputationActions.HousingPublished,
                ReputationAwardKey.Published(ReputationActions.HousingPublished, "housing", integrationEvent.ListingId),
                "housing",
                integrationEvent.ListingId,
                integrationEvent.CountryCode,
                cancellationToken)
            : Task.CompletedTask;
    }
}

/// <summary>Pays the seller, as a member, 5 XP for listing something for sale.</summary>
public sealed class ListingPublishedReputationConsumer : IntegrationEventHandler<ListingPublishedIntegrationEvent>
{
    private readonly ActivityReputationScorer _scorer;

    public ListingPublishedReputationConsumer(ActivityReputationScorer scorer) => _scorer = scorer;

    protected override Task HandleAsync(ListingPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return integrationEvent.SellerUserId is { } seller
            ? _scorer.ScoreAsync(
                seller,
                ReputationActions.ListingPublished,
                ReputationAwardKey.Published(ReputationActions.ListingPublished, "listing", integrationEvent.ListingId),
                "listing",
                integrationEvent.ListingId,
                integrationEvent.CountryCode,
                cancellationToken)
            : Task.CompletedTask;
    }
}

/// <summary>Pays the author 15 XP when a neighbourhood insight is approved.</summary>
public sealed class NeighborhoodInsightApprovedReputationConsumer
    : IntegrationEventHandler<NeighborhoodInsightApprovedIntegrationEvent>
{
    private readonly ActivityReputationScorer _scorer;

    public NeighborhoodInsightApprovedReputationConsumer(ActivityReputationScorer scorer) => _scorer = scorer;

    protected override Task HandleAsync(
        NeighborhoodInsightApprovedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return _scorer.ScoreAsync(
            integrationEvent.AuthorId,
            ReputationActions.NeighborhoodInsightApproved,
            ReputationAwardKey.Published(ReputationActions.NeighborhoodInsightApproved, "insight", integrationEvent.InsightId),
            "insight",
            integrationEvent.InsightId,
            null,
            cancellationToken);
    }
}

/// <summary>Pays the volunteer 15 XP when an organizer confirms their hours, once per assignment.</summary>
public sealed class VolunteerHoursConfirmedReputationConsumer
    : IntegrationEventHandler<VolunteerHoursConfirmedIntegrationEvent>
{
    private readonly ActivityReputationScorer _scorer;

    public VolunteerHoursConfirmedReputationConsumer(ActivityReputationScorer scorer) => _scorer = scorer;

    protected override Task HandleAsync(
        VolunteerHoursConfirmedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return _scorer.ScoreAsync(
            integrationEvent.VolunteerUserId,
            ReputationActions.VolunteerHoursConfirmed,
            ReputationAwardKey.Published(ReputationActions.VolunteerHoursConfirmed, "volunteering", integrationEvent.AssignmentId),
            "volunteering",
            integrationEvent.AssignmentId,
            null,
            cancellationToken);
    }
}

/// <summary>
/// Pays a service's owner 2 trust XP when somebody recommends it. Never the recommender, and never an
/// owner recommending their own service.
/// </summary>
public sealed class ProviderRecommendationReputationConsumer
    : IntegrationEventHandler<ProviderRecommendationReceivedIntegrationEvent>
{
    private readonly ActivityReputationScorer _scorer;

    public ProviderRecommendationReputationConsumer(ActivityReputationScorer scorer) => _scorer = scorer;

    protected override Task HandleAsync(
        ProviderRecommendationReceivedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return integrationEvent.OwnerUserId is { } owner && owner != integrationEvent.RecommenderId
            ? _scorer.ScoreAsync(
                owner,
                ReputationActions.ProviderRecommendationReceived,
                ReputationAwardKey.ProviderRecommendation(integrationEvent.RecommendationId),
                "provider",
                integrationEvent.ProviderId,
                null,
                cancellationToken)
            : Task.CompletedTask;
    }
}

/// <summary>
/// Pays a member 5 trust XP when a moderator upholds a report they raised, in any of the five
/// modules that take reports (REP-13). Never for reporting their own content.
/// <para>
/// <b>Sourced from the report, not the content.</b> Removing content takes back every award sourced
/// from it, and an upheld report usually removes the content: an award sourced from the content
/// would be taken back the moment it was paid.
/// </para>
/// </summary>
public sealed class ReportUpheldReputationConsumer
    : IntegrationEventHandler<ContentReportUpheldIntegrationEvent>
{
    private readonly ActivityReputationScorer _scorer;

    public ReportUpheldReputationConsumer(ActivityReputationScorer scorer) => _scorer = scorer;

    protected override Task HandleAsync(
        ContentReportUpheldIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return integrationEvent.ContentOwnerUserId == integrationEvent.ReporterUserId
            ? Task.CompletedTask
            : _scorer.ScoreAsync(
                integrationEvent.ReporterUserId,
                ReputationActions.ReportResolved,
                ReputationAwardKey.Report(integrationEvent.Module, integrationEvent.ReportId),
                "report",
                integrationEvent.ReportId,
                null,
                cancellationToken);
    }
}
