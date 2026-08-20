using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Domain.ValueObjects;

/// <summary>
/// What the provider is called.
/// <para>
/// A value object rather than a string because it is validated in four places — registration,
/// editing, the search projection and the category alias resolver — and a rule enforced in four
/// places is a rule enforced inconsistently. The client's field is <c>title</c>; B7 calls it a
/// name, because a provider has a name and only a listing has a title.
/// </para>
/// <para>
/// Whitespace is collapsed rather than merely trimmed. "Mama   Afrika  Kitchen" and
/// "Mama Afrika Kitchen" are the same business, and leaving them distinct produces two entries
/// in a directory and two slugs that differ invisibly.
/// </para>
/// </summary>
public sealed class ProviderName : ValueObject
{
    public const int MinLength = 2;

    public const int MaxLength = 200;

    private ProviderName(string value) => Value = value;

    private ProviderName() { }

    public string Value { get; private set; } = string.Empty;

    public static Result<ProviderName> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<ProviderName>(ProviderErrors.NameEmpty);
        }

        var normalized = Normalize(value);

        if (normalized.Length < MinLength)
        {
            return Result.Failure<ProviderName>(ProviderErrors.NameEmpty);
        }

        return normalized.Length > MaxLength
            ? Result.Failure<ProviderName>(ProviderErrors.NameTooLong)
            : Result.Success(new ProviderName(normalized));
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <summary>Trims, then collapses every internal whitespace run to a single space.</summary>
    private static string Normalize(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
