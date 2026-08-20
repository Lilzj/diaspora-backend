using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;

namespace Diaspora.Modules.Marketplace.Application.Catalog;

/// <summary>
/// The structured fields a category expects.
/// <para>
/// <b>The read the create form is generated from.</b> A client asks for a category's definitions
/// and renders a field per definition, of the kind the definition names — which is the whole reason
/// attributes are data rather than columns: a new category arrives with its form already built.
/// </para>
/// </summary>
public sealed record ListCategoryAttributesQuery
    : IQuery<IReadOnlyList<ListingAttributeDefinitionResponse>>
{
    public required string CategoryKey { get; init; }
}
