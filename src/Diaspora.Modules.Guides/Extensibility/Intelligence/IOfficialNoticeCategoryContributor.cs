namespace Diaspora.Modules.Guides.Extensibility.Intelligence;

/// <summary>
/// How a deployment adds a kind of official notice.
/// <para>
/// <b>The brief's "future categories must be configurable" made literal.</b> The twenty categories
/// B8.2 ships cover what generalises — embassy notices, visa advisories, public holidays, emergency
/// alerts and the rest — and every deployment has one nobody else needs. A Caribbean deployment wants
/// hurricane advisories with an emergency alert family. A Gulf deployment wants labour-card renewals.
/// Neither is a release of this module.
/// </para>
/// <para>
/// <b>What a contributed category can decide is deliberately bounded.</b> It sets a label, an icon, a
/// position, a default severity and an alert family — the things an editorial team owns. It cannot
/// introduce a new lifecycle, a new permission or a new publication contract, because those are the
/// platform's guarantees about what "official" means and a category is not the place to renegotiate
/// them.
/// </para>
/// <para>
/// Assembly-scanned and scoped, like every seam on this module. A contributor with an unusable key is
/// skipped and logged rather than failing the host — B8's category seeder and B8.1's catalogue seeder
/// both make the same call, because a bad seed row must not take Identity, Trust and every vertical
/// down with it.
/// </para>
/// </summary>
public interface IOfficialNoticeCategoryContributor
{
    /// <summary>The module contributing. Appears in diagnostics and in the failure log.</summary>
    string Module { get; }

    /// <summary>
    /// Lower runs first, and first wins on a repeated key.
    /// <para>
    /// The shipped contributor sits at 100, so a deployment that wants to redefine one of the
    /// standard categories registers below it — which is the only way to change a shipped row's
    /// default severity without editing this module.
    /// </para>
    /// </summary>
    int Order => 100;

    IEnumerable<NoticeCategorySeed> GetCategories();
}
