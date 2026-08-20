using Diaspora.Modules.Guides.Domain.Policies;

namespace Diaspora.Modules.Guides.Domain.Guides;

/// <summary>
/// One normalized tag on one guide.
/// <para>
/// The slug is normalized at assignment — lower-cased, hyphenated, deduplicated — because tags
/// exist to be matched, and "Bank Account", "bank-account" and "bank account" matching three
/// different chip pages is a discovery surface nobody can curate. Creation is internal:
/// tags are set wholesale through <c>Guide.SetTags</c>, which owns the count invariant.
/// </para>
/// </summary>
public sealed class GuideTagAssignment
{
    private GuideTagAssignment(string slug, int order)
    {
        Slug = slug;
        Order = order;
    }

    private GuideTagAssignment() { }

    public string Slug { get; private set; } = string.Empty;

    public int Order { get; private set; }

    /// <summary>Normalizes free text to a tag slug, or null when nothing survives.</summary>
    internal static string? NormalizeSlug(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var builder = new System.Text.StringBuilder(value.Length);
        var previousWasDash = false;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasDash = false;
                continue;
            }

            if (!previousWasDash && builder.Length > 0)
            {
                builder.Append('-');
                previousWasDash = true;
            }
        }

        var slug = builder.ToString().Trim('-');

        return slug.Length == 0
            ? null
            : slug.Length <= GuideLimits.MaxTagLength
                ? slug
                : slug[..GuideLimits.MaxTagLength].TrimEnd('-');
    }

    internal static GuideTagAssignment Create(string slug, int order) => new(slug, order);
}
