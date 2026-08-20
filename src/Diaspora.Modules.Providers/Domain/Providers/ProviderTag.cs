using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Policies;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Providers;

/// <summary>
/// One free-text tag on a provider.
/// <para>
/// The client's <c>tags: ['Ghanaian', 'Halal', 'Takeaway']</c>, which the provider detail screen
/// renders as chips. Kept as a first-class row rather than a delimited column because tags are
/// searched — "halal" is a query members type — and a <c>LIKE '%halal%'</c> over a joined string
/// matches "halalal" and cannot use an index.
/// </para>
/// <para>
/// <see cref="Label"/> is what the provider typed, casing and all, because "Halal" reads better
/// on a chip than "halal". <see cref="Slug"/> is what is compared and indexed, so "Halal",
/// "halal" and " HALAL " are one tag. Storing only one of the two would mean either ugly chips
/// or duplicate tags, and the pair costs one short column.
/// </para>
/// <para>
/// Distinct from <see cref="Categories.ServiceCategory"/>: a category is curated, hierarchical,
/// translated and browsable; a tag is whatever the provider wanted to say. Both feed search, and
/// conflating them would either make the category tree unmanageable or make tags unsearchable.
/// </para>
/// </summary>
public sealed class ProviderTag : Entity<ProviderTagId>
{
    private ProviderTag(ProviderTagId id, string label, string slug) : base(id)
    {
        Label = label;
        Slug = slug;
    }

    private ProviderTag() { }

    /// <summary>What the provider typed. Rendered on the chip.</summary>
    public string Label { get; private set; } = string.Empty;

    /// <summary>Lower-cased and hyphenated. Compared, indexed and matched against.</summary>
    public string Slug { get; private set; } = string.Empty;

    /// <summary>Null when the tag was typed by a person; set when a classifier suggested it.</summary>
    public string? Source { get; private set; }

    /// <summary>
    /// Builds a tag, or null when the input reduces to nothing.
    /// <para>
    /// Null rather than a <c>Result</c> because tags arrive as a list from a form and a single
    /// empty entry must not fail the whole edit — the caller filters nulls out, which is the
    /// behaviour a person adding four tags and leaving a fifth blank expects.
    /// </para>
    /// </summary>
    public static ProviderTag? Create(ProviderTagId id, string? label, string? source = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (string.IsNullOrWhiteSpace(label))
        {
            return null;
        }

        var trimmed = label.Trim();

        if (trimmed.Length > ProviderLimits.MaxTagLength)
        {
            trimmed = trimmed[..ProviderLimits.MaxTagLength].Trim();
        }

        var slug = Slugify(trimmed);

        return slug.Length == 0
            ? null
            : new ProviderTag(id, trimmed, slug)
            {
                Source = string.IsNullOrWhiteSpace(source) ? null : source.Trim()
            };
    }

    private static string Slugify(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length);
        var previousWasDash = false;

        foreach (var character in value.ToLowerInvariant())
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

        return builder.ToString().Trim('-');
    }
}
