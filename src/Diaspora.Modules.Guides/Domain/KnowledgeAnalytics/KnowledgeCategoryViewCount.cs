namespace Diaspora.Modules.Guides.Domain.KnowledgeAnalytics;

/// <summary>
/// How often one category was opened on one country's directory.
/// <para>
/// <b>The row that answers "what does this diaspora actually need".</b> A country whose Housing row
/// is opened four times as often as anything else is a country where the platform should be
/// commissioning housing guides, and that is a decision nobody can make from an aggregate view
/// count. Per-country-per-category is the smallest grain that supports it and the largest grain
/// that stays cheap: one row per row on a menu, not one per reader.
/// </para>
/// <para>
/// No visitor identity is recorded here, deliberately. Counting how many times a category was
/// opened needs no idea who opened it, and a directory that logged which member read about
/// immigration would be holding something it has no business holding.
/// </para>
/// </summary>
public sealed class KnowledgeCategoryViewCount
{
    private KnowledgeCategoryViewCount(string knowledgeCategoryKey)
    {
        KnowledgeCategoryKey = knowledgeCategoryKey;
    }

    private KnowledgeCategoryViewCount() { }

    /// <summary>The catalogue key. Half of the row's identity.</summary>
    public string KnowledgeCategoryKey { get; private set; } = string.Empty;

    public long Views { get; private set; }

    public DateTimeOffset? LastViewedAt { get; private set; }

    internal static KnowledgeCategoryViewCount Start(string knowledgeCategoryKey) =>
        new(knowledgeCategoryKey);

    internal void Record(DateTimeOffset at)
    {
        Views++;
        LastViewedAt = at;
    }
}
