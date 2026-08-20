using Diaspora.Modules.Guides.Extensibility.Categories;

namespace Diaspora.Modules.Guides.Infrastructure.Categories;

/// <summary>
/// Seeds the catalogue every diaspora deployment needs on day one.
/// <para>
/// <b>Twenty-one categories, ordered by what a newcomer opens first.</b> Immigration, employment
/// and housing lead because that is the actual sequence of arriving somewhere: papers, income, a
/// roof. Alphabetical would put banking above immigration, which is nobody's first week.
/// </para>
/// <para>
/// Contributed rather than migrated, which is the point of the seam: a later vertical that brings
/// a whole body of knowledge — Jobs bringing interview guides, Housing bringing tenancy guides —
/// ships its own contributor and this file gains no line. Seeding matches on
/// <see cref="GuideCategorySeed.Key"/>, so a redeploy neither duplicates the catalogue nor
/// overwrites a name staff edited in production.
/// </para>
/// <para>
/// Order zero: B8's own catalogue is the base every other contributor adds to.
/// </para>
/// </summary>
public sealed class DiasporaGuideCategoryContributor : IGuideCategoryContributor
{
    public string Module => GuidesModuleMetadata.Name;

    public int Order => 0;

    public IEnumerable<GuideCategorySeed> GetCategories()
    {
        yield return new GuideCategorySeed
        {
            Key = "immigration",
            Name = "Immigration & Visas",
            Description = "Visas, residence permits, citizenship routes and border paperwork.",
            Icon = "passport",
            Order = 10
        };

        yield return new GuideCategorySeed
        {
            Key = "employment",
            Name = "Work & Employment",
            Description = "Finding work, contracts, workers' rights and recognition of qualifications.",
            Icon = "briefcase",
            Order = 20
        };

        yield return new GuideCategorySeed
        {
            Key = "housing",
            Name = "Housing & Accommodation",
            Description = "Renting, buying, tenancy rights and finding somewhere to live.",
            Icon = "home",
            Order = 30
        };

        yield return new GuideCategorySeed
        {
            Key = "healthcare",
            Name = "Health & Healthcare",
            Description = "Registering with a doctor, insurance, prescriptions and mental health support.",
            Icon = "heart-pulse",
            Order = 40
        };

        yield return new GuideCategorySeed
        {
            Key = "education",
            Name = "Education & Training",
            Description = "Schools, universities, language courses and recognising foreign qualifications.",
            Icon = "graduation-cap",
            Order = 50
        };

        yield return new GuideCategorySeed
        {
            Key = "transportation",
            Name = "Transport & Driving",
            Description = "Public transport, driving licences, vehicle registration and getting around.",
            Icon = "bus",
            Order = 60
        };

        yield return new GuideCategorySeed
        {
            Key = "banking",
            Name = "Banking & Money",
            Description = "Opening an account, remittances, credit history and everyday payments.",
            Icon = "bank",
            Order = 70
        };

        yield return new GuideCategorySeed
        {
            Key = "taxes",
            Name = "Tax & Social Security",
            Description = "Tax numbers, filing, social contributions and double-taxation rules.",
            Icon = "receipt",
            Order = 80
        };

        yield return new GuideCategorySeed
        {
            Key = "business",
            Name = "Starting a Business",
            Description = "Registering a company, licences, self-employment and hiring.",
            Icon = "storefront",
            Order = 90
        };

        yield return new GuideCategorySeed
        {
            Key = "legal",
            Name = "Legal & Rights",
            Description = "Your rights, finding a lawyer, courts and dealing with disputes.",
            Icon = "scale",
            Order = 100
        };

        yield return new GuideCategorySeed
        {
            Key = "culture",
            Name = "Culture & Integration",
            Description = "Language, customs, etiquette and settling into a new community.",
            Icon = "globe",
            Order = 110
        };

        yield return new GuideCategorySeed
        {
            Key = "lifestyle",
            Name = "Daily Life",
            Description = "Food, leisure, faith, sport and the practicalities of everyday living.",
            Icon = "sun",
            Order = 120
        };

        yield return new GuideCategorySeed
        {
            Key = "safety",
            Name = "Safety & Security",
            Description = "Staying safe, avoiding scams, reporting crime and personal security.",
            Icon = "shield",
            Order = 130
        };

        yield return new GuideCategorySeed
        {
            Key = "emergency",
            Name = "Emergencies",
            Description = "Emergency numbers, urgent medical care, consular help and crisis support.",
            Icon = "siren",
            Order = 140
        };

        yield return new GuideCategorySeed
        {
            Key = "government-services",
            Name = "Government Services",
            Description = "Registering with authorities, official documents and dealing with public offices.",
            Icon = "building-columns",
            Order = 150
        };

        yield return new GuideCategorySeed
        {
            Key = "shopping",
            Name = "Shopping & Consumer",
            Description = "Where to buy what, consumer rights, returns and finding familiar goods.",
            Icon = "shopping-bag",
            Order = 160
        };

        yield return new GuideCategorySeed
        {
            Key = "utilities",
            Name = "Utilities & Connectivity",
            Description = "Electricity, water, internet, mobile plans and setting up a household.",
            Icon = "plug",
            Order = 170
        };

        yield return new GuideCategorySeed
        {
            Key = "childcare",
            Name = "Family & Childcare",
            Description = "Nurseries, schooling, child benefits, parental leave and raising children abroad.",
            Icon = "stroller",
            Order = 180
        };

        yield return new GuideCategorySeed
        {
            Key = "technology",
            Name = "Technology & Digital",
            Description = "Digital identity, online government portals, apps and staying connected.",
            Icon = "device-mobile",
            Order = 190
        };

        yield return new GuideCategorySeed
        {
            Key = "community",
            Name = "Community & Networks",
            Description = "Diaspora associations, places of worship, volunteering and meeting people.",
            Icon = "users",
            Order = 200
        };

        yield return new GuideCategorySeed
        {
            Key = "other",
            Name = "Other",
            Description = "Knowledge that does not fit the categories above — yet.",
            Icon = "dots",

            // Last, permanently. "Other" is where a guide goes when the catalogue has not caught
            // up with what people are writing about, and a high order keeps it out of the way.
            Order = 1_000
        };
    }
}
