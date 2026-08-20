using System.Globalization;
using System.Text;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Domain.ValueObjects;

/// <summary>
/// A normalised hashtag.
/// <para>
/// Normalisation is the entire reason this type exists. A hashtag is the one identifier on the
/// platform a member types freehand, and without a canonical form the same tag arrives as
/// LagosTraffic, lagostraffic, Lagos-Traffic and lagos_traffic, splitting one trend four ways
/// and making the trending list wrong rather than merely imprecise.
/// </para>
/// <para>
/// The rule: strip a leading hash, decompose accents to their base letters, lower-case
/// invariantly, drop everything that is not a letter or a digit. It is deliberately lossy and
/// deliberately not reversible — the display form is stored separately on the Hashtag
/// aggregate, so a member sees LagosTraffic while every count aggregates on lagostraffic.
/// </para>
/// <para>
/// Invariant lower-casing rather than culture-aware, because a Turkish server and an English
/// server must produce the same slug for the same tag or the same trend splits by deployment
/// region. This is the dotted-I problem, and it is a real one for a platform whose members are
/// spread across every locale by definition.
/// </para>
/// </summary>
public sealed class HashtagSlug : ValueObject
{
    public const int MaxLength = 64;

    public const int MinLength = 2;

    private HashtagSlug(string value) => Value = value;

    private HashtagSlug() { }

    public string Value { get; private set; } = string.Empty;

    public static Result<HashtagSlug> Create(string? raw)
    {
        var normalised = Normalise(raw);

        if (normalised.Length < MinLength)
        {
            return Result.Failure<HashtagSlug>(CommunityErrors.HashtagTooShort(MinLength));
        }

        return normalised.Length > MaxLength
            ? Result.Failure<HashtagSlug>(CommunityErrors.HashtagTooLong(MaxLength))
            : Result.Success(new HashtagSlug(normalised));
    }

    /// <summary>
    /// The canonical form of a raw tag. Public because the hashtag processor pipeline needs to
    /// normalise candidates it has extracted before deciding whether they are new.
    /// </summary>
    public static string Normalise(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var trimmed = raw.Trim().TrimStart('#');
        var decomposed = trimmed.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
