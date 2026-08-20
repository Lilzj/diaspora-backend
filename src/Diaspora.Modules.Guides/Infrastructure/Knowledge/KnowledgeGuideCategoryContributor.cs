using Diaspora.Modules.Guides.Extensibility.Categories;

namespace Diaspora.Modules.Guides.Infrastructure.Knowledge;

/// <summary>
/// The four filing categories the Knowledge Directory's navigation needs and B8 did not have.
/// <para>
/// <b>Contributed through B8's own seam rather than added to its contributor, which is the seam
/// working as designed.</b> B8 documents that "a later vertical that brings a whole body of
/// knowledge ships its own contributor and this file gains no line"; B8.1 is the first thing to take
/// it up on that. The alternative — editing <c>DiasporaGuideCategoryContributor</c> — would have
/// been a one-line diff and would have made the seam decorative.
/// </para>
/// <para>
/// <b>Why these four and not the whole navigation.</b> A knowledge category is what a country page
/// shows; a guide category is where an author files. Most of the directory's rows map cleanly onto
/// filing categories that already exist. These four do not, and in each case authors are genuinely
/// writing the guides with nowhere to put them: a first-week orientation guide, a cost-of-living
/// breakdown, a guide for arriving on a study visa, and one for retiring somewhere. Today all four
/// end up filed under <c>other</c>, which is exactly the symptom of a missing category.
/// </para>
/// <para>
/// Order 10, after B8's own base catalogue at zero.
/// </para>
/// </summary>
public sealed class KnowledgeGuideCategoryContributor : IGuideCategoryContributor
{
    public string Module => GuidesModuleMetadata.Name;

    public int Order => 10;

    public IEnumerable<GuideCategorySeed> GetCategories()
    {
        yield return new GuideCategorySeed
        {
            Key = "getting-started",
            Name = "Getting Started",

            // Deliberately the lowest order of any category: it is the first thing anybody needs
            // and the last thing anybody thinks to write.
            Description = "Your first days and weeks: what to do first, in what order, and why.",
            Icon = "compass",
            Order = 5
        };

        yield return new GuideCategorySeed
        {
            Key = "cost-of-living",
            Name = "Cost of Living",
            Description = "What things actually cost: rent, food, transport, bills and what a salary buys.",
            Icon = "coins",
            Order = 75
        };

        yield return new GuideCategorySeed
        {
            Key = "student-life",
            Name = "Student Life",
            Description = "Arriving on a study visa: enrolment, accommodation, working hours and staying on.",
            Icon = "backpack",
            Order = 55
        };

        yield return new GuideCategorySeed
        {
            Key = "retirement",
            Name = "Retirement",
            Description = "Retiring abroad: pensions, residence, healthcare entitlements and estate matters.",
            Icon = "hourglass",
            Order = 185
        };
    }
}
