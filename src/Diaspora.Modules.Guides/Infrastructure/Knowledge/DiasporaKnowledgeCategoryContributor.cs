using Diaspora.Modules.Guides.Extensibility.Knowledge;

namespace Diaspora.Modules.Guides.Infrastructure.Knowledge;

/// <summary>
/// The navigation every country directory starts from — twenty-four rows, in the order somebody
/// actually needs them.
/// <para>
/// <b>Ordered by the sequence of arriving somewhere, not alphabetically.</b> Getting started, then
/// papers, then work, then a roof, then a doctor. Alphabetical would put Banking above Immigration,
/// which is nobody's first week and would make the most important page on the platform read like a
/// filing cabinet.
/// </para>
/// <para>
/// <b>Each row names the filing categories it draws from, and several draw from more than one.</b>
/// That is the whole reason navigation and filing are separate concepts here: "Employment" is one
/// row to a reader and is legitimately fed by employment guides and business guides both, and
/// forcing authors to file into navigation would mean re-filing every guide the first time somebody
/// reworded a menu.
/// </para>
/// <para>
/// <b>Provider categories are keys and nothing more.</b> Which immigration consultants exist in
/// Mauritius is B7's question; this says only that immigration is a subject where people need one.
/// </para>
/// <para>
/// Order zero: this is the base every other contributor adds to.
/// </para>
/// </summary>
public sealed class DiasporaKnowledgeCategoryContributor : IKnowledgeCategoryContributor
{
    public string Module => GuidesModuleMetadata.Name;

    public int Order => 0;

    public IEnumerable<KnowledgeCategorySeed> GetCategories()
    {
        yield return new KnowledgeCategorySeed
        {
            Key = "getting-started",
            Name = "Getting Started",
            Description = "What to do first, in what order, and why.",
            Icon = "compass",
            Order = 10,
            SourceCategoryKeys = ["getting-started"]
        };

        yield return new KnowledgeCategorySeed
        {
            Key = "immigration",
            Name = "Immigration",
            Description = "Visas, residence permits, citizenship routes and border paperwork.",
            Icon = "passport",
            Order = 20,
            SourceCategoryKeys = ["immigration"],
            ProviderCategoryKeys = ["immigration-consultant", "lawyer"]
        };

        yield return new KnowledgeCategorySeed
        {
            Key = "employment",
            Name = "Jobs & Employment",
            Description = "Finding work, contracts, rights and getting qualifications recognised.",
            Icon = "briefcase",
            Order = 30,
            SourceCategoryKeys = ["employment"],
            ProviderCategoryKeys = ["recruiter", "lawyer"]
        };

        yield return new KnowledgeCategorySeed
        {
            Key = "housing",
            Name = "Housing",
            Description = "Renting, buying, tenancy rights and finding somewhere to live.",
            Icon = "home",
            Order = 40,
            SourceCategoryKeys = ["housing"],
            ProviderCategoryKeys = ["property-agent", "lawyer"]
        };

        yield return new KnowledgeCategorySeed
        {
            Key = "healthcare",
            Name = "Healthcare",
            Description = "Registering with a doctor, insurance, prescriptions and mental health.",
            Icon = "heart-pulse",
            Order = 50,
            SourceCategoryKeys = ["healthcare"],
            ProviderCategoryKeys = ["healthcare-provider", "insurance-broker"]
        };

        yield return new KnowledgeCategorySeed
        {
            Key = "education",
            Name = "Education",
            Description = "Schools, universities, language courses and recognising qualifications.",
            Icon = "graduation-cap",
            Order = 60,
            SourceCategoryKeys = ["education"],
            ProviderCategoryKeys = ["school", "tutor"]
        };

        yield return new KnowledgeCategorySeed
        {
            Key = "banking",
            Name = "Banking",
            Description = "Opening an account, remittances, credit history and everyday payments.",
            Icon = "bank",
            Order = 70,
            SourceCategoryKeys = ["banking"],
            ProviderCategoryKeys = ["bank", "money-transfer"]
        };

        yield return new KnowledgeCategorySeed
        {
            Key = "cost-of-living",
            Name = "Cost of Living",
            Description = "What things actually cost, and what a salary here buys.",
            Icon = "coins",
            Order = 80,
            SourceCategoryKeys = ["cost-of-living"]
        };

        yield return new KnowledgeCategorySeed
        {
            Key = "transportation",
            Name = "Transportation",
            Description = "Public transport, driving licences, vehicles and getting around.",
            Icon = "bus",
            Order = 90,
            SourceCategoryKeys = ["transportation"]
        };

        yield return new KnowledgeCategorySeed
        {
            Key = "taxes",
            Name = "Taxes",
            Description = "Tax numbers, filing, social contributions and double-taxation rules.",
            Icon = "receipt",
            Order = 100,
            SourceCategoryKeys = ["taxes"],
            ProviderCategoryKeys = ["accountant", "tax-advisor"]
        };

        yield return new KnowledgeCategorySeed
        {
            Key = "business",
            Name = "Business",
            Description = "Registering a company, licences, self-employment and hiring.",
            Icon = "storefront",
            Order = 110,
            SourceCategoryKeys = ["business"],
            ProviderCategoryKeys = ["accountant", "lawyer", "business-consultant"]
        };

        yield return new KnowledgeCategorySeed
        {
            Key = "legal",
            Name = "Legal",
            Description = "Your rights, finding a lawyer, courts and dealing with disputes.",
            Icon = "scale",
            Order = 120,
            SourceCategoryKeys = ["legal"],
            ProviderCategoryKeys = ["lawyer", "notary"]
        };

        yield return new KnowledgeCategorySeed
        {
            Key = "government",
            Name = "Government Services",
            Description = "Registering with authorities, official documents and public offices.",
            Icon = "building-columns",
            Order = 130,
            SourceCategoryKeys = ["government-services"]
        };

        yield return new KnowledgeCategorySeed
        {
            Key = "safety",
            Name = "Safety",
            Description = "Staying safe, avoiding scams, reporting crime and personal security.",
            Icon = "shield",
            Order = 140,
            SourceCategoryKeys = ["safety"]
        };

        yield return new KnowledgeCategorySeed
        {
            Key = "emergency",
            Name = "Emergency Services",
            Description = "Emergency numbers, urgent care, consular help and crisis support.",
            Icon = "siren",
            Order = 150,
            SourceCategoryKeys = ["emergency"]
        };

        yield return new KnowledgeCategorySeed
        {
            Key = "utilities",
            Name = "Utilities",
            Description = "Electricity, water, internet, mobile plans and setting up a household.",
            Icon = "plug",
            Order = 160,
            SourceCategoryKeys = ["utilities"]
        };

        yield return new KnowledgeCategorySeed
        {
            Key = "family",
            Name = "Family Life",
            Description = "Nurseries, schooling, benefits, parental leave and raising children abroad.",
            Icon = "stroller",
            Order = 170,
            SourceCategoryKeys = ["childcare"]
        };

        yield return new KnowledgeCategorySeed
        {
            Key = "student-life",
            Name = "Student Life",
            Description = "Arriving on a study visa: enrolment, housing, working hours and staying on.",
            Icon = "backpack",
            Order = 180,

            // Two sources, because a student needs both the student-specific guidance and the
            // education system it sits inside. This is the pairing the whole source model exists for.
            SourceCategoryKeys = ["student-life", "education"]
        };

        yield return new KnowledgeCategorySeed
        {
            Key = "shopping",
            Name = "Shopping",
            Description = "Where to buy what, consumer rights, returns and finding familiar goods.",
            Icon = "shopping-bag",
            Order = 190,
            SourceCategoryKeys = ["shopping"]
        };

        yield return new KnowledgeCategorySeed
        {
            Key = "culture",
            Name = "Culture",
            Description = "Language, customs, etiquette and settling into a new community.",
            Icon = "globe",
            Order = 200,
            SourceCategoryKeys = ["culture"]
        };

        yield return new KnowledgeCategorySeed
        {
            Key = "lifestyle",
            Name = "Lifestyle",
            Description = "Food, leisure, faith, sport and the practicalities of everyday living.",
            Icon = "sun",
            Order = 210,
            SourceCategoryKeys = ["lifestyle"]
        };

        yield return new KnowledgeCategorySeed
        {
            Key = "community",
            Name = "Community",
            Description = "Diaspora associations, places of worship, volunteering and meeting people.",
            Icon = "users",
            Order = 220,
            SourceCategoryKeys = ["community"]
        };

        yield return new KnowledgeCategorySeed
        {
            Key = "technology",
            Name = "Technology",
            Description = "Digital identity, government portals, apps and staying connected.",
            Icon = "device-mobile",
            Order = 230,
            SourceCategoryKeys = ["technology"]
        };

        yield return new KnowledgeCategorySeed
        {
            Key = "retirement",
            Name = "Retirement",
            Description = "Pensions, residence, healthcare entitlements and estate matters.",
            Icon = "hourglass",
            Order = 240,
            SourceCategoryKeys = ["retirement"],
            ProviderCategoryKeys = ["financial-advisor", "lawyer"]
        };
    }
}
