using System.Globalization;
using System.Text;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Domain.ValueObjects;

/// <summary>
/// The stable handle a deep link uses.
/// <para>
/// <b>Allocated once and never recomputed from the title.</b> A seller correcting a typo must not
/// break every link they have shared, every message that quotes one, and every search engine
/// result pointing at the listing. The slug is also the cursor tie-break for every listing
/// listing in the module: unique, indexed and a string, because the primary key is a
/// value-converted identifier EF cannot compare in SQL.
/// </para>
/// <para>
/// Diacritics are folded rather than dropped, so "Bébé Poussette" becomes
/// <c>bebe-poussette</c> and not <c>bb-poussette</c>. On a platform whose listings are written in
/// French, Portuguese, Polish and Yorùbá, dropping them would make a large fraction of handles
/// unreadable.
/// </para>
/// </summary>
public sealed class ListingSlug : ValueObject
{
    private ListingSlug(string value) => Value = value;

    private ListingSlug() { }

    public string Value { get; private set; } = string.Empty;

    public static Result<ListingSlug> Create(string? value)
    {
        var normalized = Normalize(value);

        return normalized is null
            ? Result.Failure<ListingSlug>(MarketplaceErrors.SlugInvalid)
            : Result.Success(new ListingSlug(normalized));
    }

    /// <summary>
    /// A slug candidate built from whatever text is to hand. Never fails — the allocator is what
    /// resolves collisions and what refuses a candidate that normalises to nothing.
    /// </summary>
    public static string Slugify(params string?[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        var joined = string.Join('-', parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        var folded = Fold(joined);
        var builder = new StringBuilder(folded.Length);
        var lastWasHyphen = true;

        foreach (var character in folded)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                lastWasHyphen = false;
            }
            else if (!lastWasHyphen)
            {
                builder.Append('-');
                lastWasHyphen = true;
            }
        }

        var slug = builder.ToString().Trim('-');

        return slug.Length > MarketplaceLimits.MaxSlug
            ? slug[..MarketplaceLimits.MaxSlug].Trim('-')
            : slug;
    }

    /// <summary>The normalised form, or null when it could never be a handle.</summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var slug = Slugify(value);

        return slug.Length is > 0 and <= MarketplaceLimits.MaxSlug ? slug : null;
    }

    /// <summary>
    /// Strips diacritics by decomposing and dropping the combining marks. Culture-invariant on
    /// purpose: a handle must be the same string whatever locale the server happens to run in.
    /// </summary>
    private static string Fold(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
