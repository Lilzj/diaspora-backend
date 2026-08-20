using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.KnowledgeAnalytics;
using Diaspora.Modules.Guides.Extensibility.Analytics;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Guides.Infrastructure.Knowledge;

/// <summary>
/// The sink that makes the country dashboard work with no external analytics configured.
/// <para>
/// <b>Folds every signal into the per-country counters, and nothing more.</b> No time series, no
/// visitor, no session — the questions the brief asks (which countries draw interest, which subjects
/// get opened, whether the directory leads anybody to read anything) are all answered by running
/// totals. A deployment that wants a real time series registers a warehouse sink alongside this one
/// and gets both.
/// </para>
/// <para>
/// <b>Counters raise no domain events, which is the rule this whole path exists to respect.</b> A
/// country page viewed ten thousand times produces ten thousand increments and zero messages. B8
/// records the same rule for guide views; the reason is that an event per view turns a popular
/// country into a self-inflicted denial of service against the outbox.
/// </para>
/// <para>
/// It saves its own work. Signals are recorded on a read path, where no command handler is going to
/// call <c>SaveChanges</c> afterwards — and an increment nobody saved is an increment that did not
/// happen.
/// </para>
/// </summary>
public sealed class CountryCounterAnalyticsSink : IKnowledgeAnalyticsSink
{
    private readonly ICountryKnowledgeAnalyticsRepository _analytics;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;

    public CountryCounterAnalyticsSink(
        ICountryKnowledgeAnalyticsRepository analytics,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids)
    {
        _analytics = analytics;
        _unitOfWork = unitOfWork;
        _guids = guids;
    }

    public string Name => "country-counters";

    public async Task RecordAsync(
        KnowledgeAnalyticsSignal signal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signal);

        if (string.IsNullOrWhiteSpace(signal.CountryCode))
        {
            // A signal with no country has nowhere to be counted. Global search phrases are still
            // recorded, by the search term sink, against the empty-country row.
            return;
        }

        var counters = await _analytics.GetAsync(signal.CountryCode, cancellationToken);

        if (counters is null)
        {
            counters = CountryKnowledgeAnalytics.Start(
                CountryKnowledgeAnalyticsId.From(_guids.NewSequentialGuid()),
                signal.CountryCode,
                signal.At);

            await _analytics.AddAsync(counters, cancellationToken);
        }

        Apply(counters, signal);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static void Apply(CountryKnowledgeAnalytics counters, KnowledgeAnalyticsSignal signal)
    {
        switch (signal.Kind)
        {
            case KnowledgeAnalyticsSignalKinds.DirectoryViewed:
                counters.RecordDirectoryView(signal.At);
                break;

            case KnowledgeAnalyticsSignalKinds.CategoryViewed:
                counters.RecordCategoryView(signal.KnowledgeCategoryKey, signal.At);
                break;

            case KnowledgeAnalyticsSignalKinds.GuideDiscovered:
                counters.RecordGuideDiscovery(signal.At);
                break;

            case KnowledgeAnalyticsSignalKinds.GuideCompleted:
                counters.RecordGuideCompletion(signal.At);
                break;

            case KnowledgeAnalyticsSignalKinds.Searched:
                counters.RecordSearch(signal.At);
                break;

            case KnowledgeAnalyticsSignalKinds.LearningPathStarted:
                counters.RecordLearningPathStart(signal.At);
                break;

            case KnowledgeAnalyticsSignalKinds.LearningPathCompleted:
                counters.RecordLearningPathCompletion(signal.At);
                break;

            default:
                // An unknown kind — a contributed signal, or a reading-progress hook that has not
                // landed yet. Ignored rather than rejected: a sink that threw on a signal it did not
                // recognise would make adding one a breaking change for every deployment.
                break;
        }
    }
}
