using Diaspora.Modules.Marketplace.Domain.Policies;

namespace Diaspora.Modules.Marketplace.Domain.Attributes;

/// <summary>
/// One option on a choice attribute — "Petrol", "Diesel", "Hybrid", "Electric".
/// <para>
/// Owned by the definition, because a choice has no meaning apart from the attribute it belongs
/// to and no lifetime beyond it. The stored value on a listing is the <see cref="Value"/>, never
/// the label: a deployment that translates "Petrol" into French must not change what every
/// existing listing says it runs on.
/// </para>
/// </summary>
public sealed class AttributeChoice
{
    private AttributeChoice(string value, string label, int order)
    {
        Value = value;
        Label = label;
        Order = order;
    }

    private AttributeChoice() { }

    /// <summary>The stored form. Stable, and what a filter matches.</summary>
    public string Value { get; private set; } = string.Empty;

    /// <summary>What the client renders. Free to change.</summary>
    public string Label { get; private set; } = string.Empty;

    public int Order { get; private set; }

    /// <summary>Builds one choice, or null when the value could never be one.</summary>
    public static AttributeChoice? Create(string? value, string? label, int order)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        if (normalized.Length > MarketplaceLimits.MaxAttributeValue)
        {
            return null;
        }

        var text = string.IsNullOrWhiteSpace(label) ? normalized : label.Trim();

        if (text.Length > MarketplaceLimits.MaxDisplayName)
        {
            text = text[..MarketplaceLimits.MaxDisplayName];
        }

        return new AttributeChoice(normalized, text, order);
    }
}
