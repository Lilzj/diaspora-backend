using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;

namespace Diaspora.Modules.CommunityProgramme.Domain.Communities;

/// <summary>
/// A community's stable, human-readable handle.
/// <para>
/// <b>The register asked for "a slug scheme stable enough to put in a deep link".</b> That is the
/// whole specification, and it has two halves. Stable means the slug never changes once issued —
/// a renamed community keeps its slug, because a link somebody sent last year has to keep working.
/// Deep-linkable means it survives a URL without escaping: lower-case, digits, and single hyphens.
/// </para>
/// <para>
/// <b>Normalisation is lossy on purpose.</b> "Nigerians in London" and "nigerians-in-london" are
/// the same slug, and so is "Nigerians  In  London". Accents fold to their base letter, so
/// "Côte d'Ivoire" becomes "cote-d-ivoire" rather than a percent-encoded string nobody can read
/// aloud down a phone. What normalisation must never do is collapse two different communities onto
/// one slug silently — uniqueness is enforced at the repository, and this type only guarantees the
/// shape.
/// </para>
/// <para>
/// <b>Reserved words are refused rather than suffixed.</b> A community slugged "me" or "search"
/// would shadow a route, and quietly issuing "me-2" gives somebody a handle they did not choose.
/// </para>
/// </summary>
public sealed partial class CommunitySlug : ValueObject
{
    public const int MinimumLength = 3;
    public const int MaximumLength = 64;

    /// <summary>
    /// Words a slug may not take, because each already names a route segment under
    /// <c>/communities</c> or would read as one.
    /// <para>
    /// <b>This list is checked against the composed route table, not maintained by hand.</b> A
    /// community slugged <c>campaigns</c> would be minted, stored, and then shadowed: its profile
    /// URL is <c>/api/v1/communities/campaigns</c>, which the router hands to the campaign list
    /// because a literal segment beats a parameter. The community is unreachable and nothing
    /// reports it. Three words below were missing for exactly that reason and were found by adding
    /// the guardrail rather than by reading this list.
    /// </para>
    /// <para>
    /// Some entries name no route yet — <c>compare</c> and <c>suggestions</c> are the register's
    /// and are not built. Reserving ahead costs a handle nobody has asked for; not reserving costs
    /// a migration to take one back.
    /// </para>
    /// </summary>
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        // Route segments live under /api/v1/communities today.
        "announcements", "campaigns", "impact", "memberships", "organizations", "polls", "search",
        "volunteer",

        // Declared by the register, not yet mapped. See the summary.
        "compare", "suggestions", "directory", "primary",

        // Never a community, whatever the route table says.
        "me", "new", "all", "none", "admin", "api", "settings",
    };

    private CommunitySlug(string value) => Value = value;

    public string Value { get; }

    /// <summary>
    /// Reads a slug a caller supplied, refusing anything that is not already in canonical form.
    /// <para>
    /// Deliberately strict rather than normalising: a caller that sends "Nigerians In London" to a
    /// lookup route has a bug, and answering it as though it were "nigerians-in-london" hides that
    /// bug until the day two slugs collide. Use <see cref="FromName"/> to mint one.
    /// </para>
    /// </summary>
    public static Result<CommunitySlug> Create(string? value)
    {
        var candidate = value?.Trim();

        if (string.IsNullOrEmpty(candidate))
        {
            return Result.Failure<CommunitySlug>(CommunityErrors.SlugRequired);
        }

        // Reserved is checked first, and the order is the message. "me" is also too short, but
        // telling somebody who typed it that handles need three characters sends them to try "mee";
        // telling them it is reserved sends them to pick a different word.
        if (Reserved.Contains(candidate))
        {
            return Result.Failure<CommunitySlug>(CommunityErrors.SlugReserved);
        }

        if (candidate.Length is < MinimumLength or > MaximumLength)
        {
            return Result.Failure<CommunitySlug>(CommunityErrors.SlugLength);
        }

        if (!SlugPattern().IsMatch(candidate))
        {
            return Result.Failure<CommunitySlug>(CommunityErrors.SlugShape);
        }

        return Result.Success(new CommunitySlug(candidate));
    }

    /// <summary>
    /// Mints a slug from a display name.
    /// <para>
    /// Used once, when a community is created. Afterwards the slug is frozen and a rename does not
    /// touch it — see the type summary.
    /// </para>
    /// </summary>
    public static Result<CommunitySlug> FromName(string? name)
    {
        var normalised = Normalise(name);

        // A name of nothing but punctuation or non-Latin script leaves nothing to slug. That is a
        // real case — a community named in Arabic or Amharic — and it needs an explicit slug rather
        // than a transliteration this module is not qualified to invent.
        return string.IsNullOrEmpty(normalised)
            ? Result.Failure<CommunitySlug>(CommunityErrors.SlugNotDerivable)
            : Create(normalised);
    }

    /// <summary>Folds accents, lower-cases, and reduces everything else to single hyphens.</summary>
    internal static string Normalise(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var decomposed = name.Trim().Normalize(NormalizationForm.FormD);
        var folded = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            // Drop the combining marks FormD split off, keeping the base letter.
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            folded.Append(char.ToLowerInvariant(character));
        }

        var hyphenated = NonSlugCharacters().Replace(folded.ToString(), "-");
        var collapsed = RepeatedHyphens().Replace(hyphenated, "-").Trim('-');

        return collapsed.Length > MaximumLength
            ? collapsed[..MaximumLength].TrimEnd('-')
            : collapsed;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugCharacters();

    [GeneratedRegex("-{2,}")]
    private static partial Regex RepeatedHyphens();
}
