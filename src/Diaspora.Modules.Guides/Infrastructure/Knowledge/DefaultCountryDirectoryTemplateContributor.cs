using Diaspora.Modules.Guides.Extensibility.Knowledge;

namespace Diaspora.Modules.Guides.Infrastructure.Knowledge;

/// <summary>
/// The arrangement a new country directory starts from.
/// <para>
/// <b>Launching a country should not begin with a blank page.</b> Without a template a curator opens
/// a new directory to twenty-four unordered rows and spends an afternoon reproducing an arrangement
/// that is roughly the same for every country — because it is: people arriving anywhere need to get
/// oriented, sort out their right to be there, find work and somewhere to live, and then a doctor.
/// </para>
/// <para>
/// <b>Six rows start hidden, and that is the interesting part of the design.</b> Retirement, student
/// life, technology, shopping, lifestyle and community are real subjects that almost never have
/// guides on the day a country launches. Arranging them in the right place but hidden means the
/// curator un-hides one the day somebody writes the first guide, rather than deciding where it goes
/// six months later when the ordering has been forgotten. A row nobody arranged is a row nobody
/// adds.
/// </para>
/// <para>
/// Applied once, at creation, and never re-applied. A curator who moves Student Life to the top for
/// a university town keeps it there.
/// </para>
/// </summary>
public sealed class DefaultCountryDirectoryTemplateContributor
    : ICountryDirectoryTemplateContributor
{
    public string Module => GuidesModuleMetadata.Name;

    public IEnumerable<CountryDirectoryTemplate> GetTemplates()
    {
        yield return new CountryDirectoryTemplate
        {
            // No country: the default, which applies to any country a deployment opens.
            CountryCode = null,
            Order = 0,
            KnowledgeCategoryKeys =
            [
                "getting-started",
                "immigration",
                "employment",
                "housing",
                "healthcare",
                "education",
                "banking",
                "cost-of-living",
                "transportation",
                "taxes",
                "business",
                "legal",
                "government",
                "safety",
                "emergency",
                "utilities",
                "family",
                "culture"
            ],
            HiddenKeys =
            [
                "student-life",
                "shopping",
                "lifestyle",
                "community",
                "technology",
                "retirement"
            ]
        };
    }
}
