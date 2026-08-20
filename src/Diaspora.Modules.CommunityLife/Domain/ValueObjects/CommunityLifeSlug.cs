using System.Text;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Domain.ValueObjects;

/// <summary>
/// The deep-link handle for an event or an organization.
/// <para>
/// <b>Unique across deleted rows, and that matters more here than anywhere else on the
/// platform.</b> The link to a funeral, a wedding or a Sunday service is shared in family group
/// chats and pinned in messages years old. A slug reused by a different event would silently
/// redirect those links to something else entirely.
/// </para>
/// <para>
/// One type for both aggregates because the rules are identical and the uniqueness scopes are not
/// — events and organizations each have their own index — so a shared type saves a duplicate
/// without merging the namespaces.
/// </para>
/// </summary>
public sealed class CommunityLifeSlug : ValueObject
{
    private CommunityLifeSlug(string value) => Value = value;

    private CommunityLifeSlug() { }

    public string Value { get; private set; } = string.Empty;

    public static Result<CommunityLifeSlug> Create(string? value)
    {
        var slug = Slugify(value);

        return slug is null
            ? Result.Failure<CommunityLifeSlug>(CommunityLifeErrors.SlugInvalid)
            : Result.Success(new CommunityLifeSlug(slug));
    }

    /// <summary>
    /// Builds a slug from arbitrary text, appending a discriminator when one is supplied.
    /// <para>
    /// The discriminator is how the allocator resolves a collision — two associations in different
    /// cities really are both called "Community Centre" — without making the first one's link
    /// change.
    /// </para>
    /// </summary>
    public static CommunityLifeSlug FromText(string? text, string? discriminator = null)
    {
        var slug = Slugify(text) ?? "item";

        if (!string.IsNullOrWhiteSpace(discriminator))
        {
            var suffix = Slugify(discriminator);

            if (suffix is not null)
            {
                var joined = $"{slug}-{suffix}";

                slug = joined.Length > CommunityLifeLimits.MaxSlug
                    ? joined[..CommunityLifeLimits.MaxSlug].TrimEnd('-')
                    : joined;
            }
        }

        return new CommunityLifeSlug(slug);
    }

    /// <summary>
    /// Lowercase, ASCII-folded, hyphen-separated. Null when nothing usable survives, which is the
    /// case for a title written entirely in a script this folding does not handle — the caller
    /// falls back to a generated handle rather than refusing the write.
    /// </summary>
    public static string? Slugify(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var builder = new StringBuilder(value.Length);
        var lastWasSeparator = false;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(character);
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator && builder.Length > 0)
            {
                builder.Append('-');
                lastWasSeparator = true;
            }
        }

        var slug = builder.ToString().Trim('-');

        if (slug.Length == 0)
        {
            return null;
        }

        return slug.Length > CommunityLifeLimits.MaxSlug
            ? slug[..CommunityLifeLimits.MaxSlug].TrimEnd('-')
            : slug;
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
