using Diaspora.Modules.Marketplace.Domain.Policies;

namespace Diaspora.Modules.Marketplace.Domain.Listings;

/// <summary>
/// One structured fact about the item — "mileage: 84000", "size: UK 10", "colour: black".
/// <para>
/// <b>A key/value pair rather than a column, and that is the extensibility promise made
/// concrete.</b> A car needs mileage and a transmission; a dress needs a size and a colour; a
/// phone needs storage and a network lock. Columns for all of them would be a schema change per
/// category and a table nobody can read; this is one narrow table a category attribute
/// contributor can extend without a migration.
/// </para>
/// <para>
/// <b>The value is always a string, whatever the definition's kind says.</b> The kind is what the
/// client renders an input for and what the validator checks against — see
/// <c>AttributeValueKind</c>. A numeric attribute additionally stores its parsed form in
/// <see cref="NumericValue"/>, so "under 100,000 miles" is an indexed range comparison rather than
/// a string sort that puts 9,000 after 80,000.
/// </para>
/// </summary>
public sealed class ListingAttribute
{
    private ListingAttribute(
        string key, string value, decimal? numericValue, string? displayName, int order)
    {
        Key = key;
        Value = value;
        NumericValue = numericValue;
        DisplayName = displayName;
        Order = order;
    }

    private ListingAttribute() { }

    /// <summary>The normalised handle a definition and a filter both name. Lower-case, hyphenated.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>The value as the seller entered it, normalised for whitespace only.</summary>
    public string Value { get; private set; } = string.Empty;

    /// <summary>The parsed form, when the value is a number. What a range filter compares.</summary>
    public decimal? NumericValue { get; private set; }

    /// <summary>The label to render. Null falls back to the definition's, then to the key.</summary>
    public string? DisplayName { get; private set; }

    public int Order { get; private set; }

    /// <summary>
    /// Builds one attribute, or null when the key or value could never be valid. Null rather than
    /// a <c>Result</c> because the aggregate validates a whole set at once and reports one error
    /// for the set — a per-item result would make the caller unwrap forty of them.
    /// </summary>
    public static ListingAttribute? Create(
        string? key, string? value, string? displayName, int order)
    {
        var normalizedKey = NormalizeKey(key);

        if (normalizedKey is null || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalizedValue = value.Trim();

        if (normalizedValue.Length > MarketplaceLimits.MaxAttributeValue)
        {
            return null;
        }

        var label = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();

        if (label is { Length: > MarketplaceLimits.MaxDisplayName })
        {
            label = label[..MarketplaceLimits.MaxDisplayName];
        }

        return new ListingAttribute(
            normalizedKey, normalizedValue, ParseNumeric(normalizedValue), label, order);
    }

    /// <summary>
    /// Lower-cases and hyphenates a key. The same normalisation the definition catalogue uses, so
    /// "Screen Size" and "screen-size" are one attribute rather than two.
    /// </summary>
    public static string? NormalizeKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var normalized = key.Trim().ToLowerInvariant().Replace(' ', '-');

        var valid = normalized.Length <= MarketplaceLimits.MaxAttributeKey
            && normalized.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')
            && !normalized.StartsWith('-')
            && !normalized.EndsWith('-')
            && !normalized.Contains("--", StringComparison.Ordinal);

        return valid ? normalized : null;
    }

    /// <summary>
    /// The numeric form, when there is one. Group separators are stripped because sellers type
    /// "84,000"; a value that is not a number simply has none, which is not an error.
    /// </summary>
    private static decimal? ParseNumeric(string value)
    {
        var stripped = value.Replace(",", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        return decimal.TryParse(
            stripped,
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;
    }
}
