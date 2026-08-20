namespace Diaspora.Modules.Marketplace.Contracts.V1.Requests;

/// <summary>
/// Replaces a listing's structured attributes wholesale.
/// <para>
/// Wholesale because that is how the editor works: a seller fills in the category's form and the
/// client saves it. A per-attribute API would make every save a diff protocol with a
/// partial-failure story, and would make the category's required-attribute check impossible to
/// state — it is a property of the set, not of any one entry.
/// </para>
/// </summary>
public sealed record SetListingAttributesRequest
{
    public required IReadOnlyList<ListingAttributeRequest> Attributes { get; init; }

    public int? BaseRevision { get; init; }
}
