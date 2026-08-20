namespace Diaspora.Modules.Marketplace.Extensibility.Categories;

/// <summary>
/// Contributes commerce categories to the catalogue.
/// <para>
/// <b>The brief's "categories must be configurable and hierarchical", made literal.</b> The
/// taxonomy the platform ships with comes from <c>DefaultListingCategoryContributor</c>, which is
/// an implementation of this interface and nothing more special than one. A deployment serving a
/// market the platform has not thought about — a country where motorcycle parts are their own
/// department, a diaspora where remittance-adjacent goods matter — ships a class and its
/// categories appear. No edit to this module, no migration, no registration line.
/// </para>
/// <para>
/// The seeder inserts what is missing and leaves what exists entirely alone, so a contributor is
/// idempotent by construction and a staff member's rename in production survives the next deploy.
/// </para>
/// </summary>
public interface IListingCategoryContributor
{
    /// <summary>
    /// Who is contributing. Recorded so a category's origin is traceable when two sources
    /// disagree about a key.
    /// </summary>
    string Source { get; }

    /// <summary>
    /// The categories to seed. Called once per startup, before the module serves traffic.
    /// Parents may be declared in any order — the seeder sorts by depth before it creates.
    /// </summary>
    IEnumerable<ListingCategorySeed> GetCategories();
}
