using Diaspora.Contracts.CommunityLife.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Application.Consumers.CommunityLife;

/// <summary>
/// Somebody responded to an event invitation.
/// <para>
/// <b>Only a "going" is a participation signal, and reading the status rather than the event name is
/// the whole of this consumer's care.</b> A "not going" is a response, and treating it as engagement
/// would teach the platform that a member is interested in exactly the events they declined.
/// </para>
/// <para>
/// <b>A "maybe" is recorded, at a discount.</b> It is genuinely weaker evidence than a yes and
/// genuinely stronger than silence, and the signal weight is the right place to express that rather
/// than a separate kind.
/// </para>
/// <para>
/// The going count refreshes the event's popularity either way — how many people are attending is
/// true regardless of what this particular member decided.
/// </para>
/// </summary>
public sealed class RsvpSubmittedConsumer : RecommendationEventConsumer<RsvpSubmittedIntegrationEvent>
{
    private const double SaturationAttendees = 500d;

    /// <summary>What a "maybe" is worth relative to a "going". See the class summary.</summary>
    private const decimal MaybeWeight = 0.4m;

    public RsvpSubmittedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.CommunityLife;

    protected override async Task HandleAsync(
        RsvpSubmittedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var status = integrationEvent.Status.Trim().ToLowerInvariant();

        // A decline is a response, not an interest. See the class summary.
        if (status is "going" or "maybe" or "interested")
        {
            await RecordAsync(
                Signal(
                    UserId.From(integrationEvent.AttendeeUserId),
                    SignalKind.Participated,
                    integrationEvent.RespondedAt,
                    RecommendationTargetType.Event,
                    integrationEvent.CommunityEventId,
                    weight: status == "going" ? null : MaybeWeight),
                cancellationToken);
        }

        await Projector.ProjectAsync(
            new CandidateProjectionRequest
            {
                TargetType = RecommendationTargetType.Event,
                TargetId = integrationEvent.CommunityEventId,
                SourceModule = SourceModule,
                PopularityScore = ToScore(integrationEvent.GoingCount),
                EngagementCount = integrationEvent.GoingCount,
                SourceUpdatedAt = integrationEvent.RespondedAt
            },
            cancellationToken);
    }

    private static decimal ToScore(int goingCount)
    {
        if (goingCount <= 0)
        {
            return 0m;
        }

        var score = Math.Log10(goingCount + 1) / Math.Log10(SaturationAttendees);

        return decimal.Round((decimal)Math.Clamp(score, 0d, 1d), 6);
    }
}
