using Diaspora.Modules.Jobs.Domain.Policies;

namespace Diaspora.Modules.Jobs.Domain.Postings;

/// <summary>
/// One normalised tag on one posting.
/// <para>
/// Normalised at assignment — lower-cased, hyphenated, deduplicated — because tags exist to be
/// matched, and "Night Shift", "night-shift" and "night shift" landing on three different chip
/// pages is a discovery surface nobody can curate. Tags are set wholesale through
/// <c>JobPosting.SetTags</c>, which owns the count invariant.
/// </para>
/// </summary>
public sealed class JobTagAssignment
{
    private JobTagAssignment(string slug, int order)
    {
        Slug = slug;
        Order = order;
    }

    private JobTagAssignment() { }

    public string Slug { get; private set; } = string.Empty;

    public int Order { get; private set; }

    /// <summary>Normalises free text to a tag slug, or null when nothing survives.</summary>
    public static string? NormalizeSlug(string? value)
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
            : slug.Length <= JobLimits.MaxTagLength
                ? slug
                : slug[..JobLimits.MaxTagLength].TrimEnd('-');
    }

    internal static JobTagAssignment Create(string slug, int order) => new(slug, order);
}
