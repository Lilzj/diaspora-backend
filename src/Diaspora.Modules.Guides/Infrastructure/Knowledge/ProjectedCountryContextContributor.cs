using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Domain.CountryContext;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Extensibility.Knowledge;

namespace Diaspora.Modules.Guides.Infrastructure.Knowledge;

/// <summary>
/// Turns the cross-vertical counter projection into bands on the country dashboard.
/// <para>
/// <b>This is why the dashboard works on day one.</b> The context seam lets a vertical contribute
/// its own band, and eventually several will — but no vertical has to for the country page to say
/// "142 jobs posted here, 38 homes listed". Those numbers come from a projection this module
/// maintains from other modules' <em>published integration contracts</em>, with no reference to a
/// single one of their assemblies. An architecture test holds it to that.
/// </para>
/// <para>
/// <b>The labels say "posted" and "listed" rather than "open" and "available", and that is a
/// correctness decision rather than a stylistic one.</b> Four of the five verticals publish a
/// country code on publication and none of them publishes one on withdrawal — a job closing carries
/// a job identifier, and resolving it to a country would mean querying the Jobs module, which is
/// exactly the reference this phase forbids. So the counters are cumulative publications, and the
/// labels say so. Housing is the single exception: its occupancy event does carry a country, and its
/// counter is therefore a live one.
/// </para>
/// <para>
/// The guide count is not projected at all — it is B8's own aggregate count, asked live, because
/// this module owns guides and has an exact answer available for the price of an index seek.
/// </para>
/// </summary>
public sealed class ProjectedCountryContextContributor : ICountryContextContributor
{
    private readonly ICountryContextSummaryRepository _summaries;
    private readonly KnowledgeGuideFinder _finder;

    public ProjectedCountryContextContributor(
        ICountryContextSummaryRepository summaries, KnowledgeGuideFinder finder)
    {
        _summaries = summaries;
        _finder = finder;
    }

    public string Module => GuidesModuleMetadata.Name;

    /// <summary>Zero: the shipped bands are the base a vertical's own contribution adds to.</summary>
    public int Order => 0;

    public async Task<IReadOnlyList<CountryContextBlock>> GetBlocksAsync(
        string countryCode, CancellationToken cancellationToken = default)
    {
        var blocks = new List<CountryContextBlock>();

        var guides = await _finder.CountCountryAsync(countryCode, cancellationToken);

        if (guides > 0)
        {
            blocks.Add(new CountryContextBlock
            {
                Kind = CountryContextSummary.NameOf(CountryContextKind.Guides),
                Label = "Guides",
                Count = guides,
                Route = $"{GuidesModuleMetadata.RoutePrefix}/countries/{countryCode}",
                Order = 0
            });
        }

        var summary = await _summaries.GetForReadAsync(countryCode, cancellationToken);

        if (summary is null)
        {
            return blocks;
        }

        foreach (var (kind, label, route, order) in Bands)
        {
            var count = summary.CountOf(kind);

            if (count > 0)
            {
                blocks.Add(new CountryContextBlock
                {
                    Kind = CountryContextSummary.NameOf(kind),
                    Label = label,
                    Count = count,
                    Route = string.Format(
                        System.Globalization.CultureInfo.InvariantCulture, route, countryCode),
                    Order = order
                });
            }
        }

        return blocks;
    }

    /// <summary>
    /// The five bands maintained from published contracts, with the routes a client follows.
    /// <para>
    /// The routes are the owning module's, written as literals here rather than resolved from those
    /// modules — resolving them would mean referencing them. That is a real coupling to a URL
    /// string, and it is the cheapest coupling available: a module that versions its prefix breaks a
    /// link on a dashboard, which is visible, rather than breaking a build, which would make every
    /// vertical wait for a Guides release.
    /// </para>
    /// </summary>
    private static readonly (CountryContextKind Kind, string Label, string Route, int Order)[] Bands =
    [
        (CountryContextKind.Jobs, "Jobs posted", "/api/v1/jobs?countryCode={0}", 10),
        (CountryContextKind.Housing, "Homes available", "/api/v1/housing?countryCode={0}", 20),
        (CountryContextKind.Marketplace, "Items listed", "/api/v1/marketplace/listings?countryCode={0}", 30),
        (CountryContextKind.Events, "Events announced", "/api/v1/community-life/events?countryCode={0}", 40),
        (CountryContextKind.Organizations, "Organizations", "/api/v1/community-life/organizations?countryCode={0}", 50),
        (CountryContextKind.Providers, "Service providers", "/api/v1/providers?countryCode={0}", 60)
    ];
}
