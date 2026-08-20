using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Categories;

/// <summary>
/// Another name a category also answers to.
/// <para>
/// <b>The mechanism that makes the frontend's three disjoint category vocabularies merge for
/// free.</b> The client has ten <c>mockCategories</c>, eighteen <c>OrgCategory</c> members and
/// four free-text <c>CreatorOrganization.category</c> strings, and they overlap: <c>Restaurants</c>,
/// <c>Food &amp; Restaurants</c> and <c>Food &amp; Hospitality</c> are one category under three
/// names. Registering the old names as aliases on the new node means a client that has not
/// shipped a release still resolves <c>category=Restaurants</c> to the right place.
/// </para>
/// <para>
/// It is also how a member's vocabulary reaches the curator's. Somebody searching for "barber"
/// should find "Hair &amp; Beauty", and adding an alias is a row rather than a rename that would
/// break everyone who had bookmarked the old name.
/// </para>
/// </summary>
public sealed class ServiceCategoryAlias : Entity<ServiceCategoryAliasId>
{
    public const int MaxValueLength = 120;

    private ServiceCategoryAlias(ServiceCategoryAliasId id, string value, string normalized)
        : base(id)
    {
        Value = value;
        Normalized = normalized;
    }

    private ServiceCategoryAlias() { }

    /// <summary>The alias as written. "Food &amp; Restaurants", "Restaurants".</summary>
    public string Value { get; private set; } = string.Empty;

    /// <summary>Lower-cased and stripped to letters and digits. What a lookup compares.</summary>
    public string Normalized { get; private set; } = string.Empty;

    /// <summary>Where the alias came from: <c>legacy-org-category</c>, <c>synonym</c>, <c>locale:fr</c>.</summary>
    public string? Source { get; private set; }

    public static ServiceCategoryAlias? Create(
        ServiceCategoryAliasId id, string? value, string? source = null)
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

        var normalized = Normalize(trimmed);

        return normalized.Length == 0
            ? null
            : new ServiceCategoryAlias(id, trimmed, normalized)
            {
                Source = string.IsNullOrWhiteSpace(source) ? null : source.Trim()
            };
    }

    /// <summary>
    /// The comparison form. Ampersands, spaces and punctuation are dropped entirely rather than
    /// replaced with hyphens, so "Food &amp; Restaurants", "Food and Restaurants" and
    /// "food-restaurants" all reduce to the same key — which is exactly the three spellings the
    /// client uses for one category today.
    /// </summary>
    public static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var builder = new System.Text.StringBuilder(value.Length);

        foreach (var character in value.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
