using Diaspora.Modules.Guides.Domain.Analytics;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Observability;
using Diaspora.Platform.Services.Abstractions;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Services;

/// <summary>
/// Records usage against the guide's analytics row, creating the row on first use.
/// <para>
/// Get-or-create is the pattern the repository documents: fetch by guide, create when absent,
/// and let the unique guide index arbitrate the race — losing one count under contention is
/// acceptable in a way losing an edit is not. Nothing here calls <c>SaveChanges</c>; the
/// handler owns the transaction, as everywhere in this module.
/// </para>
/// <para>
/// <b>Milestones are detected here</b> because this is the only place that sees both the old
/// and the new total: the view that carries the count across a power of ten (10, 100, 1000…)
/// reports it, and the handler publishes one milestone event instead of one event per view.
/// </para>
/// </summary>
public sealed class GuideAnalyticsRecorder
{
    private readonly IGuideAnalyticsRepository _analytics;
    private readonly IGuidGenerator _guids;

    /// <summary>
    /// Rows created during this request, so a second call in the same request finds the first
    /// one. The repository reads the database, which has not seen an added-but-unsaved row —
    /// and one command legitimately records twice (a view, then the completion that came with
    /// it), which without this inserts two rows and violates the one-row-per-guide index.
    /// </summary>
    private readonly Dictionary<GuideId, GuideAnalytics> _created = [];

    public GuideAnalyticsRecorder(IGuideAnalyticsRepository analytics, IGuidGenerator guids)
    {
        _analytics = analytics;
        _guids = guids;
    }

    /// <summary>
    /// Records a view and reports the milestone it crossed, when it crossed one.
    /// <para>
    /// Returns the milestone total — 10, 100, 1000… — or null for the overwhelming majority
    /// of views that cross nothing. Powers of ten from ten upward, so the event stream carries
    /// a handful of facts per popular guide rather than a fact per reader.
    /// </para>
    /// </summary>
    public async Task<long?> RecordViewAsync(
        GuideId guideId,
        bool unique,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var row = await GetOrCreateAsync(guideId, cancellationToken);

        var before = row.Views;
        row.RecordView(unique, now);
        GuideTelemetry.GuideViewsRecorded.Add(1);

        return MilestoneCrossed(before, row.Views);
    }

    /// <summary>A reader reached the end, and how long the whole read took.</summary>
    public async Task RecordCompletionAsync(
        GuideId guideId, long readSeconds, CancellationToken cancellationToken = default)
    {
        var row = await GetOrCreateAsync(guideId, cancellationToken);

        row.RecordCompletion(readSeconds);
    }

    /// <summary>Reading time reported without a completion — the reader left partway.</summary>
    public async Task RecordReadTimeAsync(
        GuideId guideId, long readSeconds, CancellationToken cancellationToken = default)
    {
        var row = await GetOrCreateAsync(guideId, cancellationToken);

        row.RecordReadTime(readSeconds);
    }

    /// <summary>The guide appeared on a search results page.</summary>
    public async Task RecordSearchImpressionsAsync(
        GuideId guideId, int count, CancellationToken cancellationToken = default)
    {
        var row = await GetOrCreateAsync(guideId, cancellationToken);

        row.RecordSearchImpressions(count);
    }

    /// <summary>A searcher clicked through to the guide.</summary>
    public async Task RecordSearchClickAsync(
        GuideId guideId, CancellationToken cancellationToken = default)
    {
        var row = await GetOrCreateAsync(guideId, cancellationToken);

        row.RecordSearchClick();
    }

    /// <summary>An old version was restored — the signal that recent edits made things worse.</summary>
    public async Task RecordVersionAdoptionAsync(
        GuideId guideId, CancellationToken cancellationToken = default)
    {
        var row = await GetOrCreateAsync(guideId, cancellationToken);

        row.RecordVersionAdoption();
    }

    /// <summary>
    /// The largest power of ten the count crossed, or null. Crossing several at once — a
    /// migration backfilling views — reports only the largest, which is the one worth a post.
    /// </summary>
    private static long? MilestoneCrossed(long before, long after)
    {
        long milestone = 10;

        long? crossed = null;

        while (milestone <= after)
        {
            if (before < milestone)
            {
                crossed = milestone;
            }

            if (milestone > long.MaxValue / 10)
            {
                break;
            }

            milestone *= 10;
        }

        return crossed;
    }

    private async Task<GuideAnalytics> GetOrCreateAsync(
        GuideId guideId, CancellationToken cancellationToken)
    {
        if (_created.TryGetValue(guideId, out var pending))
        {
            return pending;
        }

        var existing = await _analytics.GetByGuideAsync(guideId, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var created = GuideAnalytics.Create(
            GuideAnalyticsId.From(_guids.NewSequentialGuid()), guideId);

        await _analytics.AddAsync(created.Value, cancellationToken);

        _created[guideId] = created.Value;

        return created.Value;
    }
}
