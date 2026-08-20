using System.Globalization;
using System.Text;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Domain.ValueObjects;

/// <summary>
/// The stable handle a deep link carries. Allocated once and never recomputed.
/// <para>
/// <b>Never recomputed, and that is the whole rule.</b> A posting shared to a WhatsApp group
/// and forwarded for a fortnight is the normal distribution path for a job on this platform; a
/// slug that changed when the employer corrected a typo in the title would break every one of
/// those links silently. The title is editable, the slug is not.
/// </para>
/// <para>
/// It is also the cursor tie-break for every posting listing, because the primary key is a
/// value-converted <c>JobPostingId</c> that EF cannot compare in SQL — B7's lesson, which B8
/// records verbatim.
/// </para>
/// </summary>
public sealed class JobSlug : ValueObject
{
    private JobSlug(string value) => Value = value;

    private JobSlug() { }

    public string Value { get; private set; } = string.Empty;

    /// <summary>Validates a caller-supplied handle. Rejects rather than repairs.</summary>
    public static Result<JobSlug> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<JobSlug>(JobErrors.SlugInvalid);
        }

        var candidate = value.Trim().ToLowerInvariant();

        var valid = candidate.Length <= JobLimits.MaxSlug
            && candidate.Length > 0
            && candidate.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')
            && !candidate.StartsWith('-')
            && !candidate.EndsWith('-')
            && !candidate.Contains("--", StringComparison.Ordinal);

        return valid
            ? Result.Success(new JobSlug(candidate))
            : Result.Failure<JobSlug>(JobErrors.SlugInvalid);
    }

    /// <summary>
    /// Derives a handle from a title, plus whatever else the caller wants in it — usually the
    /// employer name and the city, because "nurse" is a title a thousand employers will post
    /// and the first one should not take the handle for everyone.
    /// <para>
    /// Diacritics are folded rather than dropped: "Bäcker" becomes <c>backer</c>, not
    /// <c>bcker</c>. A slug that silently loses the letters of a language is not a neutral
    /// default on a platform whose members mostly do not write in ASCII.
    /// </para>
    /// </summary>
    public static Result<JobSlug> FromParts(params string?[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        var joined = string.Join('-', parts.Where(part => !string.IsNullOrWhiteSpace(part)));

        if (string.IsNullOrWhiteSpace(joined))
        {
            return Result.Failure<JobSlug>(JobErrors.SlugInvalid);
        }

        var folded = Fold(joined);
        var builder = new StringBuilder(folded.Length);
        var lastWasSeparator = true;

        foreach (var character in folded)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator)
            {
                builder.Append('-');
                lastWasSeparator = true;
            }
        }

        var slug = builder.ToString().Trim('-');

        if (slug.Length == 0)
        {
            return Result.Failure<JobSlug>(JobErrors.SlugInvalid);
        }

        // Truncate on a boundary rather than mid-word, so a shortened handle still reads.
        if (slug.Length > JobLimits.MaxSlug)
        {
            slug = slug[..JobLimits.MaxSlug].TrimEnd('-');

            var lastSeparator = slug.LastIndexOf('-');

            if (lastSeparator > JobLimits.MaxSlug / 2)
            {
                slug = slug[..lastSeparator];
            }
        }

        return Result.Success(new JobSlug(slug));
    }

    /// <summary>
    /// The same handle with a disambiguating suffix. The allocator's collision escape.
    /// </summary>
    public JobSlug WithSuffix(string suffix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suffix);

        var trimmed = suffix.Trim('-');
        var room = JobLimits.MaxSlug - trimmed.Length - 1;
        var stem = Value.Length > room ? Value[..Math.Max(1, room)].TrimEnd('-') : Value;

        return new JobSlug($"{stem}-{trimmed}");
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <summary>Decomposes and drops combining marks, so accented letters keep their base form.</summary>
    private static string Fold(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
