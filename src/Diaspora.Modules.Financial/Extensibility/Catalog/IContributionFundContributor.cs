namespace Diaspora.Modules.Financial.Extensibility.Catalog;

/// <summary>
/// A module declaring community funds.
/// <para>
/// The brief's four — community fund, diaspora assistance, emergency relief, community projects —
/// ship as system funds. Country-specific funds are the same seam with a country code, contributed
/// by whoever operates in that country.
/// </para>
/// </summary>
public interface IContributionFundContributor
{
    string Module { get; }

    IEnumerable<ContributionFundSeed> GetFunds();
}
