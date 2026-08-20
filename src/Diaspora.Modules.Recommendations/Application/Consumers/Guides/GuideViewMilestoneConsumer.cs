using Diaspora.Contracts.Guides.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Guides;

/// <summary>
/// A guide passed a readership milestone.
/// <para>
/// <b>The only popularity feedback loop the platform currently publishes, and it is deliberately
/// coarse.</b> Guides announces a milestone rather than a running count, which means this module
/// learns that a guide is doing well without either module carrying a per-view event on the bus.
/// That is the right trade: popularity is a ranking input, and a ranking input that is a day stale
/// is fine while a ranking input that costs a message per page view is not.
/// </para>
/// <para>
/// <b>The mapping from views to a score is logarithmic, and it has to be.</b> Readership is
/// power-law distributed: the top guide in a country has thousands of times the views of the median,
/// and a linear score would put every guide but a handful at effectively zero. The log curve keeps
/// the whole corpus separable.
/// </para>
/// </summary>
public sealed class GuideViewMilestoneConsumer
    : RecommendationEventConsumer<GuideViewMilestoneReachedIntegrationEvent>
{
    /// <summary>Views at which the score saturates. Beyond this, more readers say nothing new.</summary>
    private const double SaturationViews = 100_000d;

    public GuideViewMilestoneConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Guides;

    protected override async Task HandleAsync(
        GuideViewMilestoneReachedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await Projector.ProjectAsync(
            new CandidateProjectionRequest
            {
                TargetType = RecommendationTargetType.Guide,
                TargetId = integrationEvent.GuideId,
                SourceModule = SourceModule,
                PopularityScore = ToScore(integrationEvent.TotalViews),
                EngagementCount = integrationEvent.TotalViews
            },
            cancellationToken);
    }

    /// <summary>
    /// Views to a 0–1 score, logarithmically.
    /// <para>
    /// See the class summary — readership is power-law distributed and a linear scale would collapse
    /// the whole corpus onto zero.
    /// </para>
    /// </summary>
    private static decimal ToScore(long totalViews)
    {
        if (totalViews <= 0)
        {
            return 0m;
        }

        var score = Math.Log10(totalViews + 1) / Math.Log10(SaturationViews);

        return decimal.Round((decimal)Math.Clamp(score, 0d, 1d), 6);
    }
}
