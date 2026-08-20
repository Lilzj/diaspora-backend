using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Domain.ValueObjects;

/// <summary>
/// The stable, human-readable handle a guide is linked by.
/// <para>
/// Exists so a deep link survives a retitle. <c>/guides/open-a-uk-bank-account</c> is what
/// goes into a WhatsApp message and a search result, and a guide whose title is corrected must
/// not break every one of them — so the slug is allocated once and never recomputed.
/// </para>
/// <para>
/// Uniqueness is enforced by the database, not here: a value object cannot know what other
/// values exist. The application's slug allocator asks the repository for a free slug and takes
/// a numeric suffix when the base is taken, which is why <see cref="WithSuffix"/> exists.
/// <see cref="From"/> is the trusted path for values already validated by that pipeline.
/// </para>
/// </summary>
public sealed class GuideSlug : ValueObject
{
    public const int MinLength = 2;

    private GuideSlug(string value) => Value = value;

    private GuideSlug() { }

    public string Value { get; private set; } = string.Empty;

    /// <summary>Validates a slug a caller supplied. Strict — no silent repair.</summary>
    public static Result<GuideSlug> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<GuideSlug>(GuideErrors.SlugInvalid);
        }

        var candidate = value.Trim().ToLowerInvariant();

        if (candidate.Length is < MinLength or > GuideLimits.MaxSlugLength)
        {
            return Result.Failure<GuideSlug>(GuideErrors.SlugInvalid);
        }

        var valid = candidate.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')
            && !candidate.StartsWith('-')
            && !candidate.EndsWith('-')
            && !candidate.Contains("--", StringComparison.Ordinal);

        return valid
            ? Result.Success(new GuideSlug(candidate))
            : Result.Failure<GuideSlug>(GuideErrors.SlugInvalid);
    }

    /// <summary>Wraps a value the platform already validated — a database read, never user input.</summary>
    public static GuideSlug From(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return new GuideSlug(value);
    }

    /// <summary>The same slug with a numeric suffix, for resolving a collision.</summary>
    public GuideSlug WithSuffix(int suffix)
    {
        var tail = $"-{suffix}";
        var head = Value.Length + tail.Length <= GuideLimits.MaxSlugLength
            ? Value
            : Value[..(GuideLimits.MaxSlugLength - tail.Length)].TrimEnd('-');

        return new GuideSlug(head + tail);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
