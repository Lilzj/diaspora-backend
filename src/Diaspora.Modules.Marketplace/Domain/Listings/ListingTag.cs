using Diaspora.Modules.Marketplace.Domain.Policies;

namespace Diaspora.Modules.Marketplace.Domain.Listings;

/// <summary>
/// A free-form label the seller attached — "vintage", "collection-only", "smoke-free-home".
/// <para>
/// <b>Tags are not categories, and keeping them apart is what stops the taxonomy rotting.</b> A
/// category is a place in a tree that a contributor owns and a buyer navigates; a tag is whatever
/// the seller typed. Letting sellers create categories would give the platform ten thousand of
/// them, half misspelled; refusing tags entirely would lose the one search signal sellers
/// volunteer for free.
/// </para>
/// </summary>
public sealed class ListingTag
{
    private ListingTag(string slug, int order)
    {
        Slug = slug;
        Order = order;
    }

    private ListingTag() { }

    /// <summary>Lower-case, hyphenated. The stored and indexed form.</summary>
    public string Slug { get; private set; } = string.Empty;

    public int Order { get; private set; }

    public static ListingTag Create(string slug, int order) => new(slug, order);

    /// <summary>The normalised form, or null when it could never be a tag.</summary>
    public static string? NormalizeSlug(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var normalized = raw.Trim().ToLowerInvariant().Replace(' ', '-');

        var valid = normalized.Length <= MarketplaceLimits.MaxTagLength
            && normalized.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')
            && !normalized.StartsWith('-')
            && !normalized.EndsWith('-')
            && !normalized.Contains("--", StringComparison.Ordinal);

        return valid ? normalized : null;
    }
}
