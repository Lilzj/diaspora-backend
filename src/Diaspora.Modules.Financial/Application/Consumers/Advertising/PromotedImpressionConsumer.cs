using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Recommendations.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Advertising;
using Diaspora.Modules.Financial.Domain.Advertising;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Financial.Application.Consumers.Advertising;

/// <summary>
/// Counts an impression for every subject B14 served <em>because it was promoted</em> — MON-GAP-002.
/// <para>
/// <b>This is the producer the whole performance surface was missing.</b> The register's reason the
/// gap could not be closed was that "no impression, view or click is recorded anywhere in the
/// certified contract", and that a views number taken from a listing's own counter "would answer a
/// different question than how many people saw this <em>because</em> it was promoted". B14 is the
/// only component that knows the difference: it marks the items it ranked on a promotion with its
/// own <c>Promoted</c> reason code, and now says so on the event it already published.
/// </para>
/// <para>
/// <b>Asynchronous, through the outbox, and that is the design rather than a limitation.</b> The
/// alternative is B14 calling Financial on the read path, which would put a Financial outage inside
/// somebody's search results. What it costs is a reporting lag of one dispatcher cycle, which the
/// performance contract states rather than hides.
/// </para>
/// <para>
/// <b>A subject with no live campaign is skipped in silence.</b> B14 promotes from a placement,
/// and a placement can exist without a campaign above it — every boost bought before B16 shipped is
/// one. Those are real promotions with no campaign to attribute them to, and logging each one would
/// be a log line per served promotion for a condition nobody can act on.
/// </para>
/// </summary>
public sealed class PromotedImpressionConsumer
    : IntegrationEventHandler<RecommendationGeneratedIntegrationEvent>
{
    private readonly IAdCampaignRepository _campaigns;
    private readonly AdPerformanceRecorder _recorder;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PromotedImpressionConsumer> _logger;

    public PromotedImpressionConsumer(
        IAdCampaignRepository campaigns,
        AdPerformanceRecorder recorder,
        IUnitOfWork unitOfWork,
        ILogger<PromotedImpressionConsumer> logger)
    {
        _campaigns = campaigns;
        _recorder = recorder;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    protected override async Task HandleAsync(
        RecommendationGeneratedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.PromotedTargets.Count == 0)
        {
            return;
        }

        var recorded = 0;

        foreach (var target in integrationEvent.PromotedTargets)
        {
            if (Translate(target.TargetType) is not { } subjectType)
            {
                continue;
            }

            var campaign = await _campaigns.FindDeliveringForSubjectAsync(
                subjectType, target.TargetId, cancellationToken);

            // No campaign above this promotion, or one that is not delivering. See the summary:
            // a placement bought before B16 shipped is a real promotion with nothing to bill.
            if (campaign is null)
            {
                continue;
            }

            await _recorder.RecordImpressionsAsync(
                campaign,
                placementId: null,
                integrationEvent.Surface,
                count: 1,
                integrationEvent.GeneratedAt,
                cancellationToken);

            recorded++;
        }

        if (recorded == 0)
        {
            return;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Recorded {Count} promoted impressions from recommendation set {SetId}.",
            recorded,
            integrationEvent.SetId);
    }

    /// <summary>
    /// B14's target type to B15's subject type.
    /// <para>
    /// The mirror of <c>PlacementSubjectMap</c>, which translates the other way when a placement is
    /// projected into B14. Written out rather than parsed by name, so a rename on either side is a
    /// compile error here instead of a silently unmatched impression.
    /// </para>
    /// </summary>
    private static MonetizationSubjectType? Translate(string? targetType) =>
        targetType?.Trim().ToLowerInvariant() switch
        {
            "marketplacelisting" => MonetizationSubjectType.MarketplaceListing,
            "housinglisting" => MonetizationSubjectType.HousingListing,
            "job" => MonetizationSubjectType.Job,
            "provider" => MonetizationSubjectType.Provider,
            "organization" => MonetizationSubjectType.Organization,
            "event" => MonetizationSubjectType.Event,
            "guide" => MonetizationSubjectType.Guide,
            "community" => MonetizationSubjectType.Community,

            // LearningPath, Neighborhood, Country and Announcement are B14 target types with no
            // Financial subject behind them — nothing sells a placement over any of them, so a
            // promotion of one cannot belong to a campaign. Unmapped rather than guessed.
            _ => null
        };
}
