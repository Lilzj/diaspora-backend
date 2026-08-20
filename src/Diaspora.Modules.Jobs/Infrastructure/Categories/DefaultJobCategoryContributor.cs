using Diaspora.Modules.Jobs.Extensibility.Categories;

namespace Diaspora.Modules.Jobs.Infrastructure.Categories;

/// <summary>
/// The employment domains the platform ships with — the brief's fourteen.
/// <para>
/// <b>An ordinary implementation of <see cref="IJobCategoryContributor"/> and nothing more
/// privileged than one.</b> That is the architectural claim: the shipped taxonomy arrives
/// through the same seam a deployment or a later module would use, so there is no "built-in"
/// path that has been tested and an "extension" path that has not.
/// </para>
/// <para>
/// The ordering is not alphabetical. It is roughly the order these sectors actually employ
/// diaspora members at scale — healthcare, hospitality, construction and skilled trades near the
/// top — because the browse page renders in this order and the default order is a product
/// decision, not a formatting one.
/// </para>
/// </summary>
public sealed class DefaultJobCategoryContributor : IJobCategoryContributor
{
    public string Source => "Diaspora.Jobs";

    public IEnumerable<JobCategorySeed> GetCategories()
    {
        var order = 0;

        yield return new JobCategorySeed
        {
            Key = "healthcare",
            Name = "Healthcare",
            Description = "Nursing, care work, medicine, allied health and support roles.",
            IconKey = "stethoscope",
            Order = order++
        };

        yield return new JobCategorySeed
        {
            Key = "hospitality",
            Name = "Hospitality",
            Description = "Hotels, catering, food service and tourism.",
            IconKey = "utensils",
            Order = order++
        };

        yield return new JobCategorySeed
        {
            Key = "construction",
            Name = "Construction",
            Description = "Building, civil engineering, surveying and site work.",
            IconKey = "hard-hat",
            Order = order++
        };

        yield return new JobCategorySeed
        {
            Key = "skilled-trades",
            Name = "Skilled Trades",
            Description = "Electrical, plumbing, welding, mechanics and licensed crafts.",
            IconKey = "wrench",
            Order = order++
        };

        yield return new JobCategorySeed
        {
            Key = "technology",
            Name = "Technology",
            Description = "Software, data, infrastructure, security and product.",
            IconKey = "code",
            Order = order++
        };

        yield return new JobCategorySeed
        {
            Key = "logistics",
            Name = "Logistics",
            Description = "Driving, warehousing, shipping, courier and supply chain.",
            IconKey = "truck",
            Order = order++
        };

        yield return new JobCategorySeed
        {
            Key = "education",
            Name = "Education",
            Description = "Teaching, lecturing, training and educational support.",
            IconKey = "graduation-cap",
            Order = order++
        };

        yield return new JobCategorySeed
        {
            Key = "manufacturing",
            Name = "Manufacturing",
            Description = "Production, assembly, quality control and industrial work.",
            IconKey = "factory",
            Order = order++
        };

        yield return new JobCategorySeed
        {
            Key = "agriculture",
            Name = "Agriculture",
            Description = "Farming, horticulture, fishing, forestry and seasonal work.",
            IconKey = "sprout",
            Order = order++
        };

        yield return new JobCategorySeed
        {
            Key = "retail",
            Name = "Retail",
            Description = "Shops, sales, merchandising and customer service.",
            IconKey = "shopping-bag",
            Order = order++
        };

        yield return new JobCategorySeed
        {
            Key = "finance",
            Name = "Finance",
            Description = "Banking, accountancy, insurance and financial services.",
            IconKey = "banknote",
            Order = order++
        };

        yield return new JobCategorySeed
        {
            Key = "government",
            Name = "Government",
            Description = "Public sector, civil service and local authority roles.",
            IconKey = "landmark",
            Order = order++
        };

        yield return new JobCategorySeed
        {
            Key = "non-profit",
            Name = "Non-profit",
            Description = "Charities, NGOs, community organisations and social enterprise.",
            IconKey = "heart-handshake",
            Order = order++
        };

        // Deliberately last and deliberately present. A posting that fits nowhere else must
        // still be publishable — a taxonomy with no escape hatch is one employers work around
        // by miscategorising, which is worse for discovery than an honest "Other".
        yield return new JobCategorySeed
        {
            Key = "other",
            Name = "Other",
            Description = "Roles that do not fit the categories above.",
            IconKey = "briefcase",
            Order = order
        };
    }
}
