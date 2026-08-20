namespace Diaspora.Modules.Marketplace.Domain.Enums;

/// <summary>
/// How a category attribute's value is meant to be read.
/// <para>
/// <b>Every value is stored as a string whatever this says.</b> The kind is what the client
/// renders an input for and what the validator checks against — "mileage" is a
/// <see cref="Number"/> so the form shows a numeric keypad and a letter is refused, not so the
/// database gets an integer column. A per-kind column set would mean a schema change every time a
/// contributor invents an attribute, which is exactly what the seam exists to avoid.
/// </para>
/// </summary>
public enum AttributeValueKind
{
    /// <summary>Free text, bounded.</summary>
    Text = 0,

    /// <summary>A number, validated as one and compared as one by the filter.</summary>
    Number = 1,

    /// <summary>Yes or no.</summary>
    Boolean = 2,

    /// <summary>One of a fixed list the definition carries.</summary>
    Choice = 3
}
