using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Domain.Attributes;

/// <summary>
/// What a category expects a seller to state — "mileage" on a car, "size" on a dress, "storage" on
/// a phone.
/// <para>
/// <b>This is the brief's "configurable attributes", and it is what makes the marketplace usable
/// rather than merely storable.</b> A buyer looking for a 128GB phone under £200 needs the platform
/// to know that storage is a thing phones have; free text in a description cannot answer that
/// query. A column per attribute cannot either, because the set changes with every category a
/// contributor adds.
/// </para>
/// <para>
/// <b>Scoped by category path, not by category key.</b> A definition attached to
/// <c>/electronics/</c> applies to every phone, laptop and television beneath it, so "brand" is
/// declared once rather than on forty leaves. That is the same prefix match the category tree uses
/// everywhere else.
/// </para>
/// <para>
/// <b><see cref="IsRequired"/> is enforced at publication, never at draft.</b> A seller filling in
/// a form over two sessions must be able to save what they have; publishing is where the listing
/// has to be able to answer the questions its category asks.
/// </para>
/// </summary>
public sealed class ListingAttributeDefinition
    : AggregateRoot<ListingAttributeDefinitionId>, IAuditableEntity
{
    private readonly List<AttributeChoice> _choices = [];

    private ListingAttributeDefinition(
        ListingAttributeDefinitionId id,
        string key,
        string label,
        string categoryPath,
        AttributeValueKind valueKind,
        DateTimeOffset createdAt)
        : base(id)
    {
        Key = key;
        Label = label;
        CategoryPath = categoryPath;
        ValueKind = valueKind;
        IsActive = true;
        CreatedAt = createdAt;
    }

    private ListingAttributeDefinition() { }

    /// <summary>The normalised handle a listing attribute and a filter both name.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>What the form labels the field.</summary>
    public string Label { get; private set; } = string.Empty;

    /// <summary>
    /// The category subtree this applies to, as a path prefix — <c>/vehicles/</c> or
    /// <c>/electronics/mobile-phones/</c>.
    /// </summary>
    public string CategoryPath { get; private set; } = string.Empty;

    public AttributeValueKind ValueKind { get; private set; }

    /// <summary>Whether a listing in this subtree must state it before publishing.</summary>
    public bool IsRequired { get; private set; }

    /// <summary>Whether the search surface offers it as a filter.</summary>
    public bool IsFilterable { get; private set; }

    /// <summary>A unit to render beside the value — "km", "GB", "kg". Never parsed.</summary>
    public string? Unit { get; private set; }

    /// <summary>Display order within the category's form.</summary>
    public int Order { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>The options, when the kind is <see cref="AttributeValueKind.Choice"/>.</summary>
    public IReadOnlyList<AttributeChoice> Choices =>
        _choices.OrderBy(choice => choice.Order).ToList().AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    /// <summary>Whether the definition applies to a category at the given path.</summary>
    public bool AppliesTo(string? categoryPath) =>
        !string.IsNullOrEmpty(categoryPath)
        && categoryPath.StartsWith(CategoryPath, StringComparison.Ordinal);

    public static Result<ListingAttributeDefinition> Create(
        ListingAttributeDefinitionId id,
        string? key,
        string? label,
        string? categoryPath,
        AttributeValueKind valueKind,
        DateTimeOffset now,
        bool isRequired = false,
        bool isFilterable = true,
        string? unit = null,
        int order = 0,
        IReadOnlyList<(string Value, string? Label)>? choices = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        var normalizedKey = ListingAttribute.NormalizeKey(key);

        if (normalizedKey is null)
        {
            return Result.Failure<ListingAttributeDefinition>(MarketplaceErrors.AttributeInvalid);
        }

        var trimmedLabel = label?.Trim();

        if (string.IsNullOrEmpty(trimmedLabel)
            || trimmedLabel.Length > MarketplaceLimits.MaxDisplayName)
        {
            return Result.Failure<ListingAttributeDefinition>(MarketplaceErrors.AttributeInvalid);
        }

        if (!Enum.IsDefined(valueKind))
        {
            return Result.Failure<ListingAttributeDefinition>(MarketplaceErrors.AttributeInvalid);
        }

        var path = NormalizePath(categoryPath);

        if (path is null)
        {
            return Result.Failure<ListingAttributeDefinition>(MarketplaceErrors.CategoryKeyInvalid);
        }

        var definition = new ListingAttributeDefinition(
            id, normalizedKey, trimmedLabel, path, valueKind, now)
        {
            IsRequired = isRequired,
            IsFilterable = isFilterable,
            Unit = string.IsNullOrWhiteSpace(unit) ? null : unit.Trim(),
            Order = order
        };

        if (choices is { Count: > 0 })
        {
            var applied = definition.SetChoices(choices);

            if (applied.IsFailure)
            {
                return Result.Failure<ListingAttributeDefinition>(applied.Error);
            }
        }

        // A choice attribute with no options is a form field nobody can fill in.
        if (valueKind == AttributeValueKind.Choice && definition._choices.Count == 0)
        {
            return Result.Failure<ListingAttributeDefinition>(
                MarketplaceErrors.AttributeValueInvalid);
        }

        return Result.Success(definition);
    }

    /// <summary>Replaces the option list wholesale.</summary>
    public Result SetChoices(IReadOnlyList<(string Value, string? Label)> choices)
    {
        ArgumentNullException.ThrowIfNull(choices);

        if (choices.Count > MarketplaceLimits.MaxAttributeChoices)
        {
            return Result.Failure(MarketplaceErrors.AttributeLimit);
        }

        var rebuilt = new List<AttributeChoice>(choices.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (value, label) in choices)
        {
            var choice = AttributeChoice.Create(value, label, rebuilt.Count);

            if (choice is null)
            {
                return Result.Failure(MarketplaceErrors.AttributeValueInvalid);
            }

            if (seen.Add(choice.Value))
            {
                rebuilt.Add(choice);
            }
        }

        _choices.Clear();
        _choices.AddRange(rebuilt);

        return Result.Success();
    }

    /// <summary>
    /// Whether a supplied value is one this definition accepts.
    /// <para>
    /// The one place attribute validation lives, so the create form, the publish check and the
    /// search filter all agree about what "84,000" means on a numeric attribute.
    /// </para>
    /// </summary>
    public bool Accepts(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return !IsRequired;
        }

        var trimmed = value.Trim();

        return ValueKind switch
        {
            AttributeValueKind.Number => decimal.TryParse(
                trimmed.Replace(",", string.Empty, StringComparison.Ordinal),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out _),

            AttributeValueKind.Boolean =>
                bool.TryParse(trimmed, out _)
                || trimmed is "0" or "1" or "yes" or "no" or "Yes" or "No",

            AttributeValueKind.Choice => _choices.Exists(choice =>
                string.Equals(choice.Value, trimmed, StringComparison.OrdinalIgnoreCase)),

            _ => trimmed.Length <= MarketplaceLimits.MaxAttributeValue
        };
    }

    public void Retire() => IsActive = false;

    public void Reinstate() => IsActive = true;

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    /// <summary>
    /// Normalises a subtree path to the slash-delimited form the category tree stores, so a
    /// contributor may supply <c>vehicles</c>, <c>/vehicles</c> or <c>/vehicles/</c>.
    /// </summary>
    public static string? NormalizePath(string? categoryPath)
    {
        if (string.IsNullOrWhiteSpace(categoryPath))
        {
            // No path means every category. A definition scoped to the whole marketplace is
            // legitimate — "condition" applies everywhere.
            return "/";
        }

        var trimmed = categoryPath.Trim().ToLowerInvariant();

        if (!trimmed.StartsWith('/'))
        {
            trimmed = '/' + trimmed;
        }

        if (!trimmed.EndsWith('/'))
        {
            trimmed += '/';
        }

        var valid = trimmed.Length <= MarketplaceLimits.MaxAreaPath
            && trimmed.All(character =>
                char.IsAsciiLetterOrDigit(character) || character is '-' or '/');

        return valid ? trimmed : null;
    }
}
