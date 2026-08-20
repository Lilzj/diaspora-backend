using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.KnowledgeAnalytics;

/// <summary>
/// How one country's knowledge directory is actually used.
/// <para>
/// <b>Counters raise no domain events, and that is the single most important thing about this
/// aggregate.</b> A country page viewed ten thousand times must produce ten thousand increments
/// and zero messages — B8 records the same rule for guide views, and the reason is that an event
/// per view turns a popular country into a self-inflicted denial of service against the outbox.
/// Milestones, when the platform wants them, are decided by a caller that noticed a threshold was
/// crossed and published one message.
/// </para>
/// <para>
/// <b>Aggregate counts, no identities, no time series.</b> There is no visitor here, no session,
/// no per-day bucket. What an editorial team needs from this is the shape of demand — which
/// countries people arrive at, which subjects they open, whether opening a directory leads to
/// reading anything — and every one of those questions is answered by a running total. A real time
/// series belongs in a warehouse, which is exactly what the analytics sink seam exists to feed.
/// </para>
/// </summary>
public sealed class CountryKnowledgeAnalytics
    : AggregateRoot<CountryKnowledgeAnalyticsId>, IAuditableEntity
{
    private readonly List<KnowledgeCategoryViewCount> _categoryBreakdown = [];

    private CountryKnowledgeAnalytics(
        CountryKnowledgeAnalyticsId id, string countryCode, DateTimeOffset createdAt)
        : base(id)
    {
        CountryCode = countryCode;
        CreatedAt = createdAt;
    }

    private CountryKnowledgeAnalytics() { }

    /// <summary>Upper-case ISO 3166-1 alpha-2. Unique — one row per country.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    /// <summary>Times the country's directory was opened. The denominator of country interest.</summary>
    public long DirectoryViews { get; private set; }

    /// <summary>Times any category on this country's directory was opened.</summary>
    public long CategoryViews { get; private set; }

    /// <summary>
    /// Times a reader opened a guide <em>from</em> the directory.
    /// <para>
    /// Distinct from the view count B8 keeps on the guide itself, which counts every arrival
    /// including search engines and direct links. This counts arrivals the directory caused, which
    /// is the only number that says whether the directory is doing its job.
    /// </para>
    /// </summary>
    public long GuideDiscoveries { get; private set; }

    /// <summary>Times a reader reached the end of a guide they opened from the directory.</summary>
    public long GuideCompletions { get; private set; }

    /// <summary>Searches run against this country's knowledge.</summary>
    public long Searches { get; private set; }

    /// <summary>Times somebody opened a learning path for this country.</summary>
    public long LearningPathStarts { get; private set; }

    /// <summary>Times somebody finished one.</summary>
    public long LearningPathCompletions { get; private set; }

    public DateTimeOffset? LastActivityAt { get; private set; }

    /// <summary>Per-category demand, for the country's editorial planning.</summary>
    public IReadOnlyList<KnowledgeCategoryViewCount> CategoryBreakdown =>
        _categoryBreakdown.AsReadOnly();

    /// <summary>
    /// How often opening a country leads to reading something. Zero when nobody has arrived yet.
    /// </summary>
    public double DiscoveryRate =>
        DirectoryViews == 0 ? 0 : (double)GuideDiscoveries / DirectoryViews;

    /// <summary>
    /// Guides opened per directory impression, counting both the country page and its category
    /// pages. The narrower discovery rate answers "did arriving here help"; this answers "is the
    /// navigation itself persuading anybody to click".
    /// </summary>
    public double ClickThroughRate =>
        DirectoryViews + CategoryViews == 0
            ? 0
            : (double)GuideDiscoveries / (DirectoryViews + CategoryViews);

    /// <summary>Of the guides the directory sent people to, how many were read to the end.</summary>
    public double CompletionRate =>
        GuideDiscoveries == 0 ? 0 : (double)GuideCompletions / GuideDiscoveries;

    /// <summary>Of the paths somebody started, how many they finished.</summary>
    public double PathCompletionRate =>
        LearningPathStarts == 0 ? 0 : (double)LearningPathCompletions / LearningPathStarts;

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static CountryKnowledgeAnalytics Start(
        CountryKnowledgeAnalyticsId id, string countryCode, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);

        return new CountryKnowledgeAnalytics(
            id, KnowledgeNormalization.Country(countryCode) ?? string.Empty, now);
    }

    public void RecordDirectoryView(DateTimeOffset at)
    {
        DirectoryViews++;
        LastActivityAt = at;
    }

    /// <summary>
    /// Records a category being opened, creating its counter row the first time.
    /// <para>
    /// The country total and the per-category row are both incremented, and the two are
    /// deliberately not derived from one another: the total has to survive a category being
    /// renamed or archived, and the breakdown has to survive the total being reset by a future
    /// retention policy.
    /// </para>
    /// </summary>
    public void RecordCategoryView(string? knowledgeCategoryKey, DateTimeOffset at)
    {
        CategoryViews++;
        LastActivityAt = at;

        var key = KnowledgeNormalization.Key(knowledgeCategoryKey);

        if (key is null)
        {
            return;
        }

        var counter = _categoryBreakdown.FirstOrDefault(
            view => string.Equals(view.KnowledgeCategoryKey, key, StringComparison.Ordinal));

        if (counter is null)
        {
            counter = KnowledgeCategoryViewCount.Start(key);
            _categoryBreakdown.Add(counter);
        }

        counter.Record(at);
    }

    public void RecordGuideDiscovery(DateTimeOffset at)
    {
        GuideDiscoveries++;
        LastActivityAt = at;
    }

    public void RecordGuideCompletion(DateTimeOffset at)
    {
        GuideCompletions++;
        LastActivityAt = at;
    }

    public void RecordSearch(DateTimeOffset at)
    {
        Searches++;
        LastActivityAt = at;
    }

    public void RecordLearningPathStart(DateTimeOffset at)
    {
        LearningPathStarts++;
        LastActivityAt = at;
    }

    public void RecordLearningPathCompletion(DateTimeOffset at)
    {
        LearningPathCompletions++;
        LastActivityAt = at;
    }

    /// <summary>Stamped by the shared persistence interceptor. Never called by a handler.</summary>
    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    /// <inheritdoc cref="SetCreated" />
    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }
}
