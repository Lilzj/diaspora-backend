using Diaspora.Modules.Guides.Domain.Policies;

namespace Diaspora.Modules.Guides.Domain.Intelligence;

/// <summary>
/// One notice category's label in one locale.
/// <para>
/// The same shape B8.1's navigation localization has, and deliberately a separate type rather than a
/// shared one. They are owned by different aggregates, sit in different tables, and have different
/// width limits; sharing the class would couple two catalogues that have no reason to move together,
/// and the platform's value-object sharing rule exists to stop exactly that.
/// </para>
/// </summary>
public sealed class NoticeCategoryLocalization
{
    private NoticeCategoryLocalization(string locale, string name, string? description)
    {
        Locale = locale;
        Name = name;
        Description = description;
    }

    private NoticeCategoryLocalization() { }

    /// <summary>A normalized BCP-47 tag. Half of the row's identity.</summary>
    public string Locale { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    internal static NoticeCategoryLocalization? TryCreate(
        string? locale, string? name, string? description)
    {
        var normalizedLocale = IntelligenceNormalization.Locale(locale);
        var trimmedName = IntelligenceNormalization.Text(
            name, IntelligenceLimits.MaxCategoryName);

        return normalizedLocale is null || trimmedName is null
            ? null
            : new NoticeCategoryLocalization(
                normalizedLocale,
                trimmedName,
                IntelligenceNormalization.Text(
                    description, IntelligenceLimits.MaxCategoryDescription));
    }
}
