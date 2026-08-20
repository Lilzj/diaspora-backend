namespace Diaspora.Modules.Guides.Extensibility.Knowledge;

/// <summary>
/// How a vertical puts a band on the country dashboard.
/// <para>
/// <b>The dependency-free half of cross-module integration, and it inverts the direction on
/// purpose.</b> B8.1 cannot reference Jobs, Housing, Marketplace or Community Life — those modules
/// consume the Knowledge Directory, and a reference the other way would make every one of them
/// queue behind a Guides release. So a vertical that wants to appear on a country dashboard ships a
/// class implementing this, in its own assembly, against its own data, and the kernel's assembly
/// scan finds it. Nothing in B8.1 changes, and nothing in B8.1 learns what a job is.
/// </para>
/// <para>
/// <b>B8.1 also ships a contributor that needs no vertical to do anything.</b> A projection
/// maintained from published integration contracts already counts jobs, homes, listings, events,
/// organizations and providers per country, and a shipped contributor turns those counters into
/// bands. That is what makes the dashboard work on day one. This seam is for a vertical that wants
/// to say something a counter cannot — "3 job fairs this month", "rent down 4% this quarter".
/// </para>
/// <para>
/// A contributor that throws is dropped and logged. A dashboard is composed of independent bands,
/// and one module's outage is not a reason to fail the country page.
/// </para>
/// </summary>
public interface ICountryContextContributor
{
    /// <summary>The module contributing. Appears in diagnostics and in the failure log.</summary>
    string Module { get; }

    /// <summary>Lower renders first. The projected counters ship at zero.</summary>
    int Order => 100;

    /// <summary>
    /// The bands this module offers for one country. Empty is normal — most modules have nothing
    /// to say about most countries.
    /// </summary>
    Task<IReadOnlyList<CountryContextBlock>> GetBlocksAsync(
        string countryCode, CancellationToken cancellationToken = default);
}
