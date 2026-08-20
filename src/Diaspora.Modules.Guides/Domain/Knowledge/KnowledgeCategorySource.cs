namespace Diaspora.Modules.Guides.Domain.Knowledge;

/// <summary>
/// One guide category a knowledge category draws its content from.
/// <para>
/// <b>This row is the entire seam between curation and content, and it is deliberately one
/// string.</b> The directory does not own guides, does not copy them, and does not maintain a
/// second taxonomy of them: it says "the Housing row on a country page is filled from guides filed
/// under <c>housing</c>", and B8 answers what those are. Every discovery query in this phase
/// resolves to a set of these keys and then asks the guide repository.
/// </para>
/// <para>
/// <b>Why a collection rather than a single key.</b> Curation and filing legitimately disagree.
/// "Getting Started" is a directory row every country needs and is not a filing category any
/// author would choose; it draws from <c>getting-started</c> and <c>orientation</c> together. A
/// one-to-one mapping would force the taxonomy to be reshaped every time the navigation was, which
/// is how a filing system stops matching what authors actually write.
/// </para>
/// <para>
/// A knowledge category with no sources draws from the guide category that shares its key, which
/// is the common case and needs no rows at all.
/// </para>
/// </summary>
public sealed class KnowledgeCategorySource
{
    private KnowledgeCategorySource(string guideCategoryKey)
    {
        GuideCategoryKey = guideCategoryKey;
    }

    private KnowledgeCategorySource() { }

    /// <summary>
    /// A guide category key, as B8 stores it. Not a foreign key: B8 deactivates categories rather
    /// than deleting them, so the key outlives the row in the picker, and a directory that lost
    /// its source when a category was retired would silently empty a shelf.
    /// </summary>
    public string GuideCategoryKey { get; private set; } = string.Empty;

    internal static KnowledgeCategorySource? TryCreate(string? guideCategoryKey)
    {
        var normalized = KnowledgeNormalization.Key(guideCategoryKey);

        return normalized is null ? null : new KnowledgeCategorySource(normalized);
    }
}
