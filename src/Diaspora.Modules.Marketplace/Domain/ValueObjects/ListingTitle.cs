using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Domain.ValueObjects;

/// <summary>
/// What the item is called.
/// <para>
/// <b>Whitespace is collapsed and the string is trimmed, and that is not cosmetic.</b> A title is
/// the primary search target and the card's only guaranteed text; "iPhone&#160;&#160;13  Pro" and
/// "iPhone 13 Pro" are the same listing to a buyer and two different rows to a LIKE match.
/// Normalising once, here, is what makes the search seam's job possible at all.
/// </para>
/// </summary>
public sealed class ListingTitle : ValueObject
{
    private ListingTitle(string value) => Value = value;

    private ListingTitle() { }

    public string Value { get; private set; } = string.Empty;

    public static Result<ListingTitle> Create(string? value)
    {
        var normalized = Normalize(value);

        return normalized is null
            ? Result.Failure<ListingTitle>(MarketplaceErrors.TitleInvalid)
            : Result.Success(new ListingTitle(normalized));
    }

    /// <summary>The normalised form, or null when it could never be a title.</summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var collapsed = string.Join(
            ' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return collapsed.Length is >= MarketplaceLimits.MinTitle and <= MarketplaceLimits.MaxTitle
            ? collapsed
            : null;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
