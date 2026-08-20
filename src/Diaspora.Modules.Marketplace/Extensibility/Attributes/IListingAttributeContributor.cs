namespace Diaspora.Modules.Marketplace.Extensibility.Attributes;

/// <summary>
/// Contributes the structured fields a category expects.
/// <para>
/// <b>The second half of the extensibility promise, and the half that makes the first half
/// useful.</b> A contributor that adds a "Motorcycles" category without adding "engine size" and
/// "mileage" has added a folder; adding both gives buyers a filter and sellers a form. Because
/// definitions are scoped by category <em>path</em>, a contributor declares "brand" once against
/// <c>/electronics/</c> rather than on every leaf beneath it.
/// </para>
/// <para>
/// Contributions are additive and idempotent: the seeder inserts what is missing, matching on
/// (key, category path), and never overwrites a definition an administrator has since edited.
/// </para>
/// </summary>
public interface IListingAttributeContributor
{
    /// <summary>Who is contributing. Recorded so a definition's origin is traceable.</summary>
    string Source { get; }

    /// <summary>The attribute definitions to seed. Called once per startup.</summary>
    IEnumerable<ListingAttributeSeed> GetAttributes();
}
