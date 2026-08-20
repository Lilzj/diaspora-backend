namespace Diaspora.Modules.Guides.Extensibility.Categories;

/// <summary>
/// How a module contributes entries to the guide category catalogue.
/// <para>
/// <b>The seam that keeps the catalogue extensible without a migration per category.</b> B8
/// seeds the tree the platform needs on day one; a later module that brings a whole vertical of
/// knowledge — Jobs bringing interview guides, Housing bringing tenancy guides — contributes
/// its own keys by shipping a class, and this file gains no line.
/// </para>
/// <para>
/// Contributions are <b>additive and idempotent</b>. The seeder matches on key: a category
/// whose key already exists is left entirely alone, so a redeploy does not duplicate the
/// catalogue and does not overwrite a name staff edited in production. A contributor that
/// wants to change an existing node does it through the catalogue API, deliberately.
/// </para>
/// </summary>
public interface IGuideCategoryContributor
{
    /// <summary>The module contributing. Appears in the seeding log and in diagnostics.</summary>
    string Module { get; }

    /// <summary>Lower runs first. B8's own catalogue seed is zero.</summary>
    int Order => 100;

    /// <summary>The categories this module contributes, parents before children where related.</summary>
    IEnumerable<GuideCategorySeed> GetCategories();
}
