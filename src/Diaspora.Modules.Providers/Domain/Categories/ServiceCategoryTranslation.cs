using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Categories;

/// <summary>
/// A category's name and description in one language.
/// <para>
/// The brief requires localized category names, and on a platform whose members speak English,
/// French, Arabic, Portuguese, Kiswahili, Hausa, Yorùbá, Igbo and Amharic — the nine languages
/// the client's own <c>mockLanguages</c> already lists — that is not an internationalisation
/// nicety. A francophone Ivorian looking for a lawyer in Paris should not have to read
/// "Finance &amp; Legal".
/// </para>
/// <para>
/// Rows rather than a resource file, because the catalogue itself is data: a category added by
/// staff next month needs translating without a deployment, and a resource file would put the
/// translation in the build and the category in the database.
/// </para>
/// </summary>
public sealed class ServiceCategoryTranslation : Entity<ServiceCategoryTranslationId>
{
    public const int MaxLanguageLength = 12;

    public const int MaxNameLength = 160;

    public const int MaxDescriptionLength = 500;

    private ServiceCategoryTranslation(
        ServiceCategoryTranslationId id, string languageCode, string name)
        : base(id)
    {
        LanguageCode = languageCode;
        Name = name;
    }

    private ServiceCategoryTranslation() { }

    /// <summary>BCP-47, lower case. "fr", "pt-br", "sw".</summary>
    public string LanguageCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public static ServiceCategoryTranslation? Create(
        ServiceCategoryTranslationId id, string? languageCode, string? name, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (string.IsNullOrWhiteSpace(languageCode) || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var code = languageCode.Trim().ToLowerInvariant();

        if (code.Length is < 2 or > MaxLanguageLength)
        {
            return null;
        }

        return new ServiceCategoryTranslation(id, code, Truncate(name.Trim(), MaxNameLength))
        {
            Description = string.IsNullOrWhiteSpace(description)
                ? null
                : Truncate(description.Trim(), MaxDescriptionLength)
        };
    }

    public void Update(string? name, string? description)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = Truncate(name.Trim(), MaxNameLength);
        }

        Description = string.IsNullOrWhiteSpace(description)
            ? null
            : Truncate(description.Trim(), MaxDescriptionLength);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
