using Diaspora.Modules.Marketplace.Domain.Attributes;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Services;

/// <summary>
/// Checks a listing's attributes against what its category asks for.
/// <para>
/// <b>The aggregate cannot do this, and that is why the service exists.</b> Whether "petrol" is a
/// legal value for "fuel-type" is a fact about the definition catalogue, and an aggregate that
/// reached into a repository to find out would be an aggregate no test could construct.
/// </para>
/// <para>
/// <b>The required check runs at publication, never at draft.</b> A seller filling in a form over
/// two sessions must be able to save what they have; publishing is where the listing has to be able
/// to answer the questions its category asks. That split is what makes an eighteen-field vehicle
/// form usable on a phone.
/// </para>
/// </summary>
public sealed class ListingAttributeValidator
{
    private readonly IListingAttributeDefinitionRepository _definitions;

    public ListingAttributeValidator(IListingAttributeDefinitionRepository definitions) =>
        _definitions = definitions;

    /// <summary>Every definition that applies to a category, ancestors included.</summary>
    public async Task<IReadOnlyList<ListingAttributeDefinition>> GetDefinitionsAsync(
        string categoryPath, CancellationToken cancellationToken = default) =>
        await _definitions.GetForCategoryPathAsync(categoryPath, cancellationToken);

    /// <summary>
    /// Checks supplied values against the definitions that apply.
    /// <para>
    /// An attribute with no definition is accepted rather than refused: a seller volunteering a
    /// fact the catalogue has not thought of is adding information, and refusing it would mean a
    /// category cannot be enriched until somebody ships a definition for it. What is refused is a
    /// value a definition explicitly rejects — a letter where a number belongs, an option that is
    /// not on the list.
    /// </para>
    /// </summary>
    public async Task<Result> ValidateAsync(
        string categoryPath,
        IReadOnlyList<ListingAttributeDraft> attributes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        var definitions = await GetDefinitionsAsync(categoryPath, cancellationToken);

        if (definitions.Count == 0)
        {
            return Result.Success();
        }

        var byKey = definitions
            .Where(definition => definition.IsActive)
            .ToDictionary(definition => definition.Key, StringComparer.Ordinal);

        foreach (var attribute in attributes)
        {
            var key = ListingAttribute.NormalizeKey(attribute?.Key);

            if (key is null)
            {
                return Result.Failure(MarketplaceErrors.AttributeInvalid);
            }

            if (byKey.TryGetValue(key, out var definition)
                && !definition.Accepts(attribute!.Value))
            {
                return Result.Failure(MarketplaceErrors.AttributeValueInvalid);
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Whether the listing states everything its category requires. The publication gate.
    /// </summary>
    public async Task<Result> ValidateForPublicationAsync(
        MarketplaceListing listing,
        string categoryPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listing);

        var definitions = await GetDefinitionsAsync(categoryPath, cancellationToken);

        var required = definitions
            .Where(definition => definition is { IsActive: true, IsRequired: true })
            .ToList();

        if (required.Count == 0)
        {
            return Result.Success();
        }

        var supplied = listing.Attributes
            .Select(attribute => attribute.Key)
            .ToHashSet(StringComparer.Ordinal);

        return required.TrueForAll(definition => supplied.Contains(definition.Key))
            ? Result.Success()
            : Result.Failure(MarketplaceErrors.AttributeRequired);
    }
}
