using Diaspora.Contracts.CommunityLife.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Application.Consumers.CommunityLife;

/// <summary>
/// A community event was published.
/// <para>
/// <b>The only target type with a natural expiry, and setting it correctly is the whole of this
/// consumer's responsibility.</b> An event that has finished is not a weak recommendation, it is a
/// wrong one — and unlike a filled job or a let flat, nobody publishes a withdrawal for it. The end
/// time is what stops the events surface filling with last month.
/// </para>
/// <para>
/// <b>The expiry is the end, not the start.</b> Somebody scrolling at eight in the evening should
/// still see the thing that began at seven — a three-hour meetup is worth joining late, and expiring
/// on the start time would remove exactly the events a member could still get to.
/// </para>
/// <para>
/// <b>The audience flags become tags.</b> Free, family-friendly, student-focused: these are what a
/// member filters on, and as tags they meet the profile's interests in the shared vocabulary rather
/// than needing four boolean columns.
/// </para>
/// </summary>
public sealed class EventPublishedConsumer
    : RecommendationEventConsumer<EventPublishedIntegrationEvent>
{
    public EventPublishedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.CommunityLife;

    protected override async Task HandleAsync(
        EventPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var attributes = new List<(InterestKind Kind, string Key)>();
        attributes.AddRange(Attributes(InterestKind.Tag, integrationEvent.Facets));
        attributes.Add((InterestKind.Tag, integrationEvent.AttendanceMode));

        if (integrationEvent.IsFree) attributes.Add((InterestKind.Tag, "free"));
        if (integrationEvent.IsFamilyFriendly) attributes.Add((InterestKind.Tag, "family-friendly"));
        if (integrationEvent.IsChildFriendly) attributes.Add((InterestKind.Tag, "child-friendly"));
        if (integrationEvent.IsStudentFocused) attributes.Add((InterestKind.Tag, "student-focused"));

        await Projector.ProjectAsync(
            new CandidateProjectionRequest
            {
                TargetType = RecommendationTargetType.Event,
                TargetId = integrationEvent.CommunityEventId,
                SourceModule = SourceModule,
                Title = integrationEvent.Title,
                Summary = integrationEvent.Summary,
                Slug = integrationEvent.Slug,
                MediaReference = integrationEvent.CoverMediaId?.ToString(),
                CategoryKey = integrationEvent.CategoryKey,
                CountryCode = integrationEvent.CountryCode,
                RegionName = integrationEvent.RegionName,
                CityName = integrationEvent.CityName,
                NeighborhoodName = integrationEvent.NeighborhoodName,
                OwnerUserId = UserId.From(integrationEvent.OrganizerUserId),
                IsVerified = integrationEvent.IsOrganizerVerified,
                PopularityScore = Jobs.JobPublishedConsumer.InitialPopularity,
                QualityScore = integrationEvent.IsOrganizerVerified ? 0.7m : 0.4m,
                Visibility = CandidateVisibility.Members,
                PublishedAt = integrationEvent.PublishedAt,
                SourceUpdatedAt = integrationEvent.PublishedAt,

                // The end, not the start. See the class summary.
                ExpiresAt = integrationEvent.EndsAt,
                Attributes = attributes,
                ReplaceAttributes = true
            },
            cancellationToken);
    }
}
