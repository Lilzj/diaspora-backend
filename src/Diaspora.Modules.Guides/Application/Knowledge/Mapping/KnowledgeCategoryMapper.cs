using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Integration;

namespace Diaspora.Modules.Guides.Application.Knowledge.Mapping;

/// <summary>
/// Turns a navigation row into the shapes the wire and the consuming modules read.
/// <para>
/// <b>Localization happens here and nowhere else.</b> The fallback rule — exact locale, then the
/// language alone, then the default — lives on the aggregate, and this is the only thing that calls
/// it. A client that picked its own translation from a list would have to reimplement that rule, and
/// a rule implemented twice is a rule implemented differently.
/// </para>
/// <para>
/// Static, because it holds no state. A registration for a type with no fields is a container entry
/// nobody resolves, and CA1822 in Release is what pushes these static in the first place.
/// </para>
/// </summary>
public static class KnowledgeCategoryMapper
{
    /// <summary>
    /// The reader-facing shape: localized, with translations omitted.
    /// <para>
    /// Translations are a curator's concern. Shipping forty of them to every reader would be forty
    /// times the payload for a label they will never see in thirty-nine of those languages.
    /// </para>
    /// </summary>
    public static KnowledgeCategoryResponse ToResponse(KnowledgeCategory category, string? locale)
    {
        ArgumentNullException.ThrowIfNull(category);

        var (name, description) = category.Localized(locale);

        return new KnowledgeCategoryResponse
        {
            KnowledgeCategoryId = category.Id.Value,
            Key = category.Key,
            Name = name,
            Description = description,
            Icon = category.Icon,
            Order = category.Order,
            IsActive = category.IsActive,
            SourceCategoryKeys = category.EffectiveSourceKeys,
            ProviderCategoryKeys =
                [.. category.ProviderCategories.Select(provider => provider.ServiceCategoryKey)],
            Revision = category.Revision
        };
    }

    /// <summary>The curator-facing shape: unlocalized defaults, with every translation.</summary>
    public static KnowledgeCategoryResponse ToCuratorResponse(KnowledgeCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);

        return ToResponse(category, locale: null) with
        {
            Localizations =
            [
                .. category.Localizations
                    .OrderBy(localization => localization.Locale, StringComparer.Ordinal)
                    .Select(localization => new KnowledgeCategoryLocalizationResponse
                    {
                        Locale = localization.Locale,
                        Name = localization.Name,
                        Description = localization.Description
                    })
            ]
        };
    }

    /// <summary>
    /// The consuming-module shape.
    /// <para>
    /// Flat primitives, and a guide count the caller supplies — the count is country-specific and
    /// the category is not, so a mapper that computed it would have to know which country was being
    /// asked about, which is exactly the coupling that makes a mapper stop being one.
    /// </para>
    /// </summary>
    public static KnowledgeCategoryDescriptor ToDescriptor(
        KnowledgeCategory category, string? locale, int guideCount)
    {
        ArgumentNullException.ThrowIfNull(category);

        var (name, description) = category.Localized(locale);

        return new KnowledgeCategoryDescriptor
        {
            Key = category.Key,
            Name = name,
            Description = description,
            Icon = category.Icon,
            Order = category.Order,
            GuideCount = guideCount,
            ProviderCategoryKeys =
                [.. category.ProviderCategories.Select(provider => provider.ServiceCategoryKey)]
        };
    }
}
