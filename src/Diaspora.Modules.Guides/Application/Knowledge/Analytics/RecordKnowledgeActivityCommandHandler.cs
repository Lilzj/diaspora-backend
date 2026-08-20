using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Extensibility.Analytics;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Knowledge.Analytics;

/// <summary>
/// Records a reader activity signal.
/// <para>
/// <b>An unrecognised kind succeeds silently.</b> A client sending a signal a later version
/// introduced, or one this deployment does not fold into any counter, is not an error — and failing
/// it would make adding a signal kind a breaking change for every client on every platform. The
/// recorder ignores what it does not know.
/// </para>
/// <para>
/// <b>A malformed country is a failure, though.</b> Everything here is counted per country, and a
/// signal with no valid country has nowhere to go — silently discarding it would make a client's
/// analytics quietly stop working with nothing to diagnose from.
/// </para>
/// </summary>
public sealed class RecordKnowledgeActivityCommandHandler
    : ICommandHandler<RecordKnowledgeActivityCommand>
{
    private readonly KnowledgeAnalyticsRecorder _analytics;
    private readonly IClock _clock;

    public RecordKnowledgeActivityCommandHandler(
        KnowledgeAnalyticsRecorder analytics, IClock clock)
    {
        _analytics = analytics;
        _clock = clock;
    }

    public async Task<Result> Handle(
        RecordKnowledgeActivityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var country = KnowledgeNormalization.Country(command.CountryCode);

        if (country is null)
        {
            return Result.Failure(KnowledgeErrors.CountryInvalid);
        }

        await _analytics.RecordAsync(
            new KnowledgeAnalyticsSignal
            {
                Kind = command.Kind,
                CountryCode = country,
                KnowledgeCategoryKey = command.Request.KnowledgeCategoryKey,
                GuideId = command.Request.GuideId,
                LearningPathId = command.Request.LearningPathId,
                Locale = command.Request.Locale,
                At = _clock.UtcNow
            },
            cancellationToken);

        return Result.Success();
    }
}
