using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Domain.Policies;

namespace Diaspora.Modules.Guides.Domain.Directories;

/// <summary>
/// One knowledge category's placement on one country's directory.
/// <para>
/// <b>The row that makes the directory country-first rather than category-first.</b> The catalogue
/// says which navigation rows exist; this says which of them Mauritius shows and in what order —
/// and those genuinely differ by country. "Student Life" belongs near the top for a country whose
/// diaspora arrives on study visas and near the bottom for one whose diaspora arrives on work
/// permits, and no single global ordering is right for both.
/// </para>
/// <para>
/// <see cref="LabelOverride"/> exists for the same reason at the level of wording: the row that is
/// "Immigration" almost everywhere is "Residency" where that is the term the local paperwork
/// actually uses, and forcing a curator to choose between the platform's vocabulary and the one
/// their readers will recognise is a choice with an obviously wrong answer.
/// </para>
/// </summary>
public sealed class CountryDirectoryEntry
{
    private CountryDirectoryEntry(
        string knowledgeCategoryKey, int position, string? labelOverride, bool isHidden)
    {
        KnowledgeCategoryKey = knowledgeCategoryKey;
        Position = position;
        LabelOverride = labelOverride;
        IsHidden = isHidden;
    }

    private CountryDirectoryEntry() { }

    /// <summary>The catalogue key this entry places. Half of the row's identity.</summary>
    public string KnowledgeCategoryKey { get; private set; } = string.Empty;

    /// <summary>Where it appears. Contiguous from zero, renumbered by the aggregate on every save.</summary>
    public int Position { get; private set; }

    /// <summary>This country's own wording for the row, or null to use the catalogue label.</summary>
    public string? LabelOverride { get; private set; }

    /// <summary>
    /// Whether the row is arranged but not shown.
    /// <para>
    /// Kept as a flag rather than by removing the entry, because a curator preparing a country
    /// launch arranges twenty rows over a fortnight and needs the eight that have no guides yet to
    /// stay where they put them. Removing and re-adding loses the position and the label every
    /// time.
    /// </para>
    /// </summary>
    public bool IsHidden { get; private set; }

    internal static CountryDirectoryEntry? TryCreate(
        string? knowledgeCategoryKey, int position, string? labelOverride, bool isHidden)
    {
        var key = KnowledgeNormalization.Key(knowledgeCategoryKey);

        return key is null
            ? null
            : new CountryDirectoryEntry(
                key,
                position,
                KnowledgeNormalization.Text(labelOverride, KnowledgeLimits.MaxCategoryName),
                isHidden);
    }

    internal void MoveTo(int position) => Position = position;
}
