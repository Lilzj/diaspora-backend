using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Extensibility.Catalog;

namespace Diaspora.Modules.Financial.Catalog;

/// <summary>
/// The community funds the platform ships: the brief's four, as system funds.
/// <para>
/// <b>All five contribution kinds are accepted by default, and that is the important line in this
/// file.</b> Most giving on this platform is not money — the client's own donation screen offers
/// goods, volunteer service, professional skills and supplies alongside it — and a fund that took
/// only money would make four fifths of that screen unrepresentable.
/// </para>
/// <para>
/// Country-specific funds are the same seam with a country code, contributed by whoever operates in
/// that country. Nothing here is exhaustive.
/// </para>
/// </summary>
public sealed class DefaultContributionFundContributor : IContributionFundContributor
{
    private const string Gbp = "GBP";

    public string Module => FinancialModuleMetadata.Name;

    public IEnumerable<ContributionFundSeed> GetFunds()
    {
        yield return new ContributionFundSeed
        {
            Code = "community.fund",
            Name = "Community Fund",
            Description =
                "The standing fund behind community projects, gatherings and shared resources.",
            Currency = Gbp,
            Urgency = ContributionUrgency.Ongoing,
            SortOrder = 10,
            IsSystem = true
        };

        yield return new ContributionFundSeed
        {
            Code = "diaspora.assistance",
            Name = "Diaspora Assistance",
            Description =
                "Direct help for members in difficulty — arrival costs, documents, emergencies.",
            Currency = Gbp,
            Urgency = ContributionUrgency.High,
            SortOrder = 20,
            IsSystem = true
        };

        yield return new ContributionFundSeed
        {
            Code = "emergency.relief",
            Name = "Emergency Relief",
            Description = "Rapid response when something happens to a community or a country.",
            Currency = Gbp,
            Urgency = ContributionUrgency.Urgent,
            SortOrder = 30,
            IsSystem = true
        };

        yield return new ContributionFundSeed
        {
            Code = "community.projects",
            Name = "Community Projects",
            Description = "Longer-term work: spaces, equipment, training and events.",
            Currency = Gbp,
            Urgency = ContributionUrgency.Moderate,
            SortOrder = 40,
            IsSystem = true
        };
    }
}
