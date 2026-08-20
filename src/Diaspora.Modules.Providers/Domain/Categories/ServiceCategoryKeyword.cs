using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Categories;

/// <summary>
/// A word that should lead a member to this category.
/// <para>
/// The brief asks for keywords alongside tags and aliases, and the distinction between the three
/// is worth stating because it is easy to collapse them: an <see cref="ServiceCategoryAlias"/>
/// is another <em>name</em> for the category and resolves exactly; a
/// <see cref="Providers.ProviderTag"/> belongs to one provider and is whatever they typed; a
/// keyword belongs to the category and exists purely to widen retrieval. "Jollof" is not a name
/// for Food &amp; Restaurants and is not a property of any one restaurant, but somebody typing
/// it should land there.
/// </para>
/// <para>
/// <b>Search does not live here.</b> B7 stores the keywords and publishes them on its search
/// projection; B15 decides what to do with them. That boundary is the brief's, and it is what
/// stops this module growing a query planner.
/// </para>
/// </summary>
public sealed class ServiceCategoryKeyword : Entity<ServiceCategoryKeywordId>
{
    public const int MaxValueLength = 80;

    private ServiceCategoryKeyword(ServiceCategoryKeywordId id, string value, string normalized)
        : base(id)
    {
        Value = value;
        Normalized = normalized;
    }

    private ServiceCategoryKeyword() { }

    public string Value { get; private set; } = string.Empty;

    /// <summary>Lower-cased and stripped. What a lookup compares.</summary>
    public string Normalized { get; private set; } = string.Empty;

    /// <summary>
    /// How strongly this keyword implies the category, 1–10. A ranking input for B15 and B17,
    /// and the reason "jollof" can be a stronger signal for Food than "delivery" is.
    /// </summary>
    public int Weight { get; private set; } = 5;

    /// <summary>The language the keyword is in, when it is language-specific.</summary>
    public string? LanguageCode { get; private set; }

    public static ServiceCategoryKeyword? Create(
        ServiceCategoryKeywordId id, string? value, int weight = 5, string? languageCode = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > MaxValueLength)
        {
            trimmed = trimmed[..MaxValueLength].Trim();
        }

        var normalized = ServiceCategoryAlias.Normalize(trimmed);

        return normalized.Length == 0
            ? null
            : new ServiceCategoryKeyword(id, trimmed, normalized)
            {
                Weight = Math.Clamp(weight, 1, 10),
                LanguageCode = string.IsNullOrWhiteSpace(languageCode)
                    ? null
                    : languageCode.Trim().ToLowerInvariant()
            };
    }
}
