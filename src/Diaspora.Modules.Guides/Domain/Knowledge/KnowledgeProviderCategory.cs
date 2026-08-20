namespace Diaspora.Modules.Guides.Domain.Knowledge;

/// <summary>
/// One provider service category a knowledge category recommends alongside its guides.
/// <para>
/// <b>How "read about immigration, then find an immigration consultant" is expressed without B8.1
/// knowing anything about providers.</b> The row holds a service category key that B7 owns; the
/// directory never stores a provider, never ranks one and never decides whether one is any good.
/// A category view returns these keys, and the client — or B7's own directory contract — resolves
/// them into actual professionals for the country being viewed.
/// </para>
/// <para>
/// Curated per knowledge category rather than per country, because the pairing is about the
/// subject: everywhere on earth, the people who help with immigration paperwork are immigration
/// consultants and lawyers. Which of them exist in Mauritius is B7's question, asked at read time.
/// </para>
/// </summary>
public sealed class KnowledgeProviderCategory
{
    private KnowledgeProviderCategory(string serviceCategoryKey)
    {
        ServiceCategoryKey = serviceCategoryKey;
    }

    private KnowledgeProviderCategory() { }

    /// <summary>A provider service category key, as B7 publishes it. Opaque here.</summary>
    public string ServiceCategoryKey { get; private set; } = string.Empty;

    internal static KnowledgeProviderCategory? TryCreate(string? serviceCategoryKey)
    {
        var normalized = KnowledgeNormalization.Key(serviceCategoryKey);

        return normalized is null ? null : new KnowledgeProviderCategory(normalized);
    }
}
