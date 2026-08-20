using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Domain.Categories;

/// <summary>
/// A category's stable, language-independent key.
/// <para>
/// <b>The slug is the contract, not the name.</b> A category's display name is translated, is
/// edited by staff, and reads differently in every locale; its slug is what a client filter, a
/// deep link, a saved search and an alias all point at. The frontend already relies on this
/// without saying so — its <c>mockCategories</c> carry both <c>id</c> and <c>slug</c>, and its
/// filter row compares ids while its routes would naturally use slugs.
/// </para>
/// <para>
/// Immutable once created. Renaming a category changes its name; changing its slug breaks every
/// link to it, which is why there is no setter and no derive-from-name path here — unlike
/// <see cref="ValueObjects.ProviderSlug"/>, a category slug is always chosen deliberately by
/// whoever curates the catalogue.
/// </para>
/// </summary>
public sealed class CategorySlug : ValueObject
{
    public const int MinLength = 2;

    public const int MaxLength = 80;

    private CategorySlug(string value) => Value = value;

    private CategorySlug() { }

    public string Value { get; private set; } = string.Empty;

    public static Result<CategorySlug> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<CategorySlug>(ProviderErrors.SlugInvalid);
        }

        var candidate = value.Trim().ToLowerInvariant();

        if (candidate.Length is < MinLength or > MaxLength)
        {
            return Result.Failure<CategorySlug>(ProviderErrors.SlugInvalid);
        }

        var valid = candidate.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')
            && !candidate.StartsWith('-')
            && !candidate.EndsWith('-')
            && !candidate.Contains("--", StringComparison.Ordinal);

        return valid
            ? Result.Success(new CategorySlug(candidate))
            : Result.Failure<CategorySlug>(ProviderErrors.SlugInvalid);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
