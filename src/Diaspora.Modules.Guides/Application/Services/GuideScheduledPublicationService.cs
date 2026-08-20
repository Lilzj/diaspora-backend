using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Versions;
using Diaspora.Modules.Guides.Observability;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Guides.Application.Services;

/// <summary>
/// Publishes the guides whose scheduled instant has passed.
/// <para>
/// A small internal service rather than logic inside the hosted publisher, so the manual
/// publish command handler can reuse the same "publish plus snapshot" sequence when it
/// arrives — one definition of what publication writes, however it is triggered.
/// </para>
/// <para>
/// Each guide is its own unit: publish, snapshot the version, save, and only then move on. A
/// guide that fails — deleted since scheduling, no longer publishable — is logged and skipped,
/// because one author's broken draft must not hold up everyone scheduled behind them. The
/// batch is bounded, so a backlog after downtime is worked through rather than swallowed
/// whole.
/// </para>
/// </summary>
internal sealed class GuideScheduledPublicationService
{
    private readonly IGuideRepository _guides;
    private readonly IGuideVersionRepository _versions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly ILogger<GuideScheduledPublicationService> _logger;

    public GuideScheduledPublicationService(
        IGuideRepository guides,
        IGuideVersionRepository versions,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        IClock clock,
        ILogger<GuideScheduledPublicationService> logger)
    {
        _guides = guides;
        _versions = versions;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Publishes everything due. Returns how many guides were published.</summary>
    public async Task<int> RunOnceAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var due = await _guides.GetScheduledDueAsync(now, batchSize, cancellationToken);

        var published = 0;

        foreach (var guide in due)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (await PublishAsync(guide, now, cancellationToken))
                {
                    published++;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // One broken draft must not hold up everyone scheduled behind it.
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Scheduled publication of guide {GuideId} failed. It will be retried next pass.",
                    guide.Id.Value);
            }
#pragma warning restore CA1031
        }

        return published;
    }

    private async Task<bool> PublishAsync(
        Guide guide, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // The snapshot is taken after Publish so the version records the reading time the
        // publication computed — the same order the manual publish handler will use.
        var publishedResult = guide.Publish(now);

        if (publishedResult.IsFailure)
        {
            // Not an exception: the guide stopped being publishable since it was scheduled —
            // emptied, archived, deleted. The schedule is cancelled so the publisher does not
            // retry a guide that will refuse forever, and the author sees the draft intact.
            _logger.LogWarning(
                "Guide {GuideId} was scheduled to publish but refused: {Error}. Cancelling the schedule.",
                guide.Id.Value,
                publishedResult.Error.Code);

            guide.CancelScheduledPublication();
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return false;
        }

        var version = GuideVersion.Create(
            GuideVersionId.From(_guids.NewSequentialGuid()),
            guide.Id,
            guide.CurrentVersion,
            guide.Title,
            guide.Summary?.Summary,
            guide.Language,
            guide.CategoryKey,
            GuideSnapshotSerializer.Serialize(guide),
            revisionNotes: null,
            guide.AuthorId,
            now,
            guide.WordCount,
            guide.Reading.ReadingTimeMinutes);

        if (version.IsFailure)
        {
            _logger.LogError(
                "Snapshotting scheduled guide {GuideId} failed: {Error}.",
                guide.Id.Value,
                version.Error.Code);

            return false;
        }

        await _versions.AddAsync(version.Value, cancellationToken);

        // Saved per guide, deliberately: a failure on the ninth guide must not roll back the
        // eight already published, and the outbox dispatches each guide's events with its own
        // committed write.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        GuideTelemetry.GuidesPublished.Add(1);
        GuideTelemetry.GuideVersionsCreated.Add(1);

        _logger.LogInformation(
            "Published scheduled guide {GuideId} as version {Version}.",
            guide.Id.Value,
            guide.CurrentVersion);

        return true;
    }
}
