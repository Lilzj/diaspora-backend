namespace Diaspora.Modules.Jobs.Extensibility.Categories;

/// <summary>
/// Contributes employment domains to the catalogue.
/// <para>
/// <b>The brief's "categories must be extensible", made literal.</b> The fourteen the platform
/// ships with come from <c>DefaultJobCategoryContributor</c>, which is an implementation of this
/// interface and nothing more special than one. A deployment serving a market the platform has
/// not thought about, or a later module that owns a taxonomy, ships a class and its categories
/// appear — no edit to this module, no migration, no registration line.
/// </para>
/// <para>
/// The seeder upserts by key on every startup, so a contributor is idempotent by construction:
/// re-seeding an existing category updates its name and ordering and leaves every posting under
/// it attached.
/// </para>
/// </summary>
public interface IJobCategoryContributor
{
    /// <summary>
    /// Who is contributing. Recorded so a category's origin is traceable when two sources
    /// disagree about a key.
    /// </summary>
    string Source { get; }

    /// <summary>
    /// The categories to seed. Called once per startup, before the module serves traffic.
    /// </summary>
    IEnumerable<JobCategorySeed> GetCategories();
}
