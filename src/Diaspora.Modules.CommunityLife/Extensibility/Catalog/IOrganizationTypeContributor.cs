namespace Diaspora.Modules.CommunityLife.Extensibility.Catalog;

/// <summary>
/// How a module or a deployment adds kinds of organization.
/// <para>
/// <b>This is what "categories must remain configurable" costs.</b> An assembly implements one
/// method, ships, and its types appear in the picker at the next startup. No enum is edited, no
/// migration is written, and this module gains no line.
/// </para>
/// <para>
/// The shipped set is contributed the same way, by a class in this assembly with no privileges the
/// others do not have — which is the only reliable way to know the seam works.
/// </para>
/// </summary>
public interface IOrganizationTypeContributor
{
    /// <summary>
    /// The types this contributor supplies. Called once at startup, by the seeder.
    /// <para>
    /// A duplicate key is not an error and does not fail the boot: the first contributor to supply
    /// a key wins, and the seeder logs the rest. Two assemblies both wanting a "church" type is a
    /// deployment doing something reasonable, and taking the host down over it would be
    /// disproportionate — unlike a duplicate feature gate, where the ambiguity is a security
    /// question.
    /// </para>
    /// </summary>
    IEnumerable<OrganizationTypeDescriptor> GetOrganizationTypes();
}
