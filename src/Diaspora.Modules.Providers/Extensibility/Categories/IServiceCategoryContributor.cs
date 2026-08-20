namespace Diaspora.Modules.Providers.Extensibility.Categories;

/// <summary>
/// How a module contributes entries to the service category catalogue.
/// <para>
/// <b>The seam that keeps the catalogue extensible without a migration per category.</b> The
/// brief requires hierarchical categories, subcategories, aliases, keywords and localized names,
/// all extensible — and B7 seeds the tree the frontend implies. A later module that introduces
/// a whole vertical (Housing brings letting agents, Jobs brings recruiters) contributes its own
/// nodes by shipping a class, and this file gains no line.
/// </para>
/// <para>
/// Contributions are <b>additive and idempotent</b>. The seeder matches on slug: a node whose
/// slug already exists is left alone, so a redeploy does not duplicate the tree and does not
/// overwrite a name staff edited in production. A contributor that wants to change an existing
/// node's name has to say so through the catalogue API, deliberately, rather than by silently
/// winning a startup race.
/// </para>
/// <para>
/// Ordering matters only in one respect: a child names its parent by slug, so a contributor
/// returning a child before its parent is fine within one contributor, but a child whose parent
/// lives in a <em>different</em> contributor needs <see cref="Order"/> to be higher than that
/// contributor's. B7's own seed runs at order zero.
/// </para>
/// </summary>
public interface IServiceCategoryContributor
{
    /// <summary>The module contributing. Appears in the seeding log and in diagnostics.</summary>
    string Module { get; }

    /// <summary>Lower runs first. B7's own catalogue seed is zero.</summary>
    int Order => 100;

    /// <summary>The nodes this module contributes, parents before children where related.</summary>
    IEnumerable<ServiceCategorySeed> GetCategories();
}
