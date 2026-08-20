using Diaspora.Modules.Providers.Extensibility.Categories;

namespace Diaspora.Modules.Providers.Infrastructure.Categories;

/// <summary>
/// The catalogue B7 seeds, reconciling the frontend's three disjoint category vocabularies.
/// <para>
/// <b>Every legacy client string is registered as an alias</b>, which is what makes the merge
/// free. The client carries ten <c>mockCategories</c> names, eighteen <c>OrgCategory</c> members
/// and four free-text creator strings; they overlap (<c>Restaurants</c> ≡
/// <c>Food &amp; Restaurants</c> ≡ "Food &amp; Hospitality") and one of them is literally
/// commented <c>// future-ready</c> where the collision was foreseen. A client that has not
/// shipped a release keeps resolving.
/// </para>
/// <para>
/// <b>Keywords are the part that will age best.</b> "jollof" is not a name for
/// Food &amp; Restaurants and is not a property of any one restaurant, but somebody typing it
/// should land there — and when B15 and B17 arrive, these are the labelled training signals they
/// inherit for free.
/// </para>
/// <para>
/// Order zero: this is the base tree every later contributor hangs children off.
/// </para>
/// </summary>
public sealed class DiasporaServiceCategoryContributor : IServiceCategoryContributor
{
    public string Module => ProvidersModuleMetadata.Name;

    public int Order => 0;

    public IEnumerable<ServiceCategorySeed> GetCategories()
    {
        foreach (var seed in FoodAndRestaurants())
        {
            yield return seed;
        }

        foreach (var seed in HairAndBeauty())
        {
            yield return seed;
        }

        foreach (var seed in HealthAndWellness())
        {
            yield return seed;
        }

        foreach (var seed in FinanceAndLegal())
        {
            yield return seed;
        }

        foreach (var seed in FaithAndWorship())
        {
            yield return seed;
        }

        foreach (var seed in CommunityAndAssociations())
        {
            yield return seed;
        }

        foreach (var seed in GovernmentAndEmbassies())
        {
            yield return seed;
        }

        foreach (var seed in Remaining())
        {
            yield return seed;
        }
    }

    private static IEnumerable<ServiceCategorySeed> FoodAndRestaurants()
    {
        yield return new ServiceCategorySeed
        {
            Slug = "food-restaurants",
            Name = "Food & Restaurants",
            Icon = "🍛",
            SortOrder = 10,
            Aliases = ["Food & Restaurants", "Food and Restaurants", "Food & Hospitality", "Food"],
            Keywords = ["jollof", "takeaway", "african food", "caribbean food", "halal", "catering", "buffet"],
            Translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["fr"] = "Alimentation et restaurants",
                ["pt"] = "Alimentação e restaurantes",
                ["ar"] = "الطعام والمطاعم",
                ["sw"] = "Chakula na Migahawa"
            }
        };

        yield return Child("restaurants", "Restaurants", "food-restaurants", 10,
            aliases: ["Restaurants"],
            keywords: ["dine in", "sit down", "eatery"]);

        yield return Child("takeaway", "Takeaway", "food-restaurants", 20,
            keywords: ["delivery", "collection", "fast food"]);

        yield return Child("catering", "Catering", "food-restaurants", 30,
            keywords: ["party", "wedding food", "event catering", "buffet"]);

        yield return Child("grocery-market", "Grocery & Market", "food-restaurants", 40,
            keywords: ["african shop", "asian shop", "provisions", "yam", "plantain"]);

        yield return Child("bakery", "Bakery & Desserts", "food-restaurants", 50,
            keywords: ["cake", "puff puff", "pastry", "chin chin"]);
    }

    private static IEnumerable<ServiceCategorySeed> HairAndBeauty()
    {
        yield return new ServiceCategorySeed
        {
            Slug = "hair-beauty",
            Name = "Hair & Beauty",
            Icon = "💇",
            SortOrder = 20,
            Aliases = ["Hair & Beauty", "Hair and Beauty", "Beauty"],
            Keywords = ["braids", "barber", "salon", "natural hair", "locs", "cornrows", "wig"],
            Translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["fr"] = "Coiffure et beauté",
                ["pt"] = "Cabelo e beleza",
                ["ar"] = "الشعر والتجميل"
            }
        };

        yield return Child("hair-braiding", "Hair Braiding", "hair-beauty", 10,
            keywords: ["box braids", "cornrows", "twists", "crochet", "knotless"]);

        yield return Child("barbers", "Barbers", "hair-beauty", 20,
            keywords: ["fade", "line up", "haircut"]);

        yield return Child("beauty-nails", "Beauty & Nails", "hair-beauty", 30,
            keywords: ["makeup", "lashes", "manicure", "pedicure"]);
    }

    private static IEnumerable<ServiceCategorySeed> HealthAndWellness()
    {
        yield return new ServiceCategorySeed
        {
            Slug = "health-wellness",
            Name = "Health & Wellness",
            Icon = "💪",
            SortOrder = 30,
            Aliases = ["Health & Wellness", "Health and Wellness", "Health"],
            Keywords = ["clinic", "surgery", "therapy", "wellbeing"],
            Translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["fr"] = "Santé et bien-être",
                ["pt"] = "Saúde e bem-estar",
                ["ar"] = "الصحة والعافية"
            }
        };

        yield return Child("doctors", "Doctors", "health-wellness", 10,
            aliases: ["Doctors"],
            keywords: ["gp", "physician", "consultant"]);

        yield return Child("clinics", "Clinics", "health-wellness", 20,
            keywords: ["walk in", "private clinic", "health centre"]);

        yield return Child("pharmacy", "Pharmacy", "health-wellness", 30,
            keywords: ["chemist", "prescription", "dispensary"]);

        yield return Child("therapy-counselling", "Therapy & Counselling", "health-wellness", 40,
            keywords: ["mental health", "counsellor", "psychotherapy", "bereavement"]);

        yield return Child("fitness", "Fitness", "health-wellness", 50,
            keywords: ["gym", "personal trainer", "dance class"]);
    }

    private static IEnumerable<ServiceCategorySeed> FinanceAndLegal()
    {
        yield return new ServiceCategorySeed
        {
            Slug = "finance-legal",
            Name = "Finance & Legal",
            Icon = "⚖️",
            SortOrder = 40,
            Aliases = ["Finance & Legal", "Finance and Legal", "Legal", "Finance"],
            Keywords = ["remittance", "solicitor", "tax", "visa", "immigration"],
            Translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["fr"] = "Finance et droit",
                ["pt"] = "Finanças e jurídico",
                ["ar"] = "المالية والقانون"
            }
        };

        yield return Child("money-transfer", "Money Transfer", "finance-legal", 10,
            keywords: ["remittance", "send money home", "exchange", "bureau de change"]);

        yield return Child("lawyers", "Lawyers", "finance-legal", 20,
            aliases: ["Lawyers"],
            keywords: ["solicitor", "barrister", "legal advice"]);

        yield return Child("accountants", "Accountants", "finance-legal", 30,
            aliases: ["Accountants"],
            keywords: ["tax return", "bookkeeping", "self assessment"]);

        yield return Child("immigration-consultants", "Immigration Consultants", "finance-legal", 40,
            aliases: ["Immigration Consultants"],
            keywords: ["visa", "settlement", "asylum", "oisc", "sponsorship"]);

        yield return Child("insurance", "Insurance", "finance-legal", 50,
            keywords: ["cover", "policy", "broker"]);
    }

    private static IEnumerable<ServiceCategorySeed> FaithAndWorship()
    {
        yield return new ServiceCategorySeed
        {
            Slug = "faith-worship",
            Name = "Faith & Worship",
            Icon = "🕌",
            SortOrder = 50,
            Aliases = ["Faith", "Places of Worship", "Religion"],
            Keywords = ["service", "prayer", "congregation", "fellowship"],
            Translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["fr"] = "Foi et culte",
                ["pt"] = "Fé e culto",
                ["ar"] = "الدين والعبادة"
            }
        };

        yield return Child("churches", "Churches", "faith-worship", 10,
            aliases: ["Churches"],
            keywords: ["sunday service", "bible study", "parish", "pentecostal", "catholic"]);

        yield return Child("mosques", "Mosques", "faith-worship", 20,
            aliases: ["Mosques"],
            keywords: ["jummah", "salah", "masjid", "islamic centre"]);

        yield return Child("temples", "Temples & Other", "faith-worship", 30,
            keywords: ["temple", "gurdwara", "synagogue", "shrine"]);
    }

    private static IEnumerable<ServiceCategorySeed> CommunityAndAssociations()
    {
        yield return new ServiceCategorySeed
        {
            Slug = "community-associations",
            Name = "Community & Associations",
            Icon = "🤝",
            SortOrder = 60,
            Aliases =
            [
                "Associations",
                "Community Centres",
                "Cultural & Diaspora Support",
                "Community"
            ],
            Keywords = ["diaspora", "association", "welfare", "hometown union", "support group"],
            Translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["fr"] = "Communauté et associations",
                ["pt"] = "Comunidade e associações",
                ["ar"] = "المجتمع والجمعيات"
            }
        };

        yield return Child("student-associations", "Student Associations", "community-associations", 10,
            aliases: ["Student Associations", "Education & Student Support"],
            keywords: ["university", "society", "campus", "freshers"]);

        yield return Child("professional-associations", "Professional Associations", "community-associations", 20,
            aliases: ["Professional Associations", "Business Associations"],
            keywords: ["network", "chamber", "guild", "cpd"]);

        yield return Child("cultural-organizations", "Cultural Organizations", "community-associations", 30,
            aliases: ["Cultural Organizations"],
            keywords: ["heritage", "language school", "dance troupe", "festival"]);

        yield return Child("ngos-charities", "NGOs & Charities", "community-associations", 40,
            aliases: ["NGOs", "Volunteer Organizations"],
            keywords: ["charity", "non profit", "volunteering", "aid"]);

        yield return Child("sports-clubs", "Sports Clubs", "community-associations", 50,
            aliases: ["Sports Clubs"],
            keywords: ["football", "basketball", "athletics", "team"]);
    }

    private static IEnumerable<ServiceCategorySeed> GovernmentAndEmbassies()
    {
        yield return new ServiceCategorySeed
        {
            Slug = "government-embassies",
            Name = "Government & Embassies",
            Icon = "🏛",
            SortOrder = 70,
            Aliases = ["Government", "Public Services"],
            Keywords = ["consulate", "passport", "civil registry", "council"],
            Translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["fr"] = "Gouvernement et ambassades",
                ["pt"] = "Governo e embaixadas",
                ["ar"] = "الحكومة والسفارات"
            }
        };

        yield return Child("embassies", "Embassies & Consulates", "government-embassies", 10,
            aliases: ["Embassies"],
            keywords: ["passport renewal", "consular", "high commission", "visa appointment"]);

        yield return Child("public-services", "Public Services", "government-embassies", 20,
            keywords: ["council", "registry", "benefits", "housing office"]);
    }

    /// <summary>
    /// The remaining <c>mockCategories</c> roots, each with the subcategories a directory
    /// actually needs to be browsable at a second level.
    /// </summary>
    private static IEnumerable<ServiceCategorySeed> Remaining()
    {
        yield return Root("retail-shopping", "Retail & Shopping", "🛍️", 80,
            aliases: ["Retail & Shopping", "Retail and Shopping", "Shopping"],
            keywords: ["clothing", "fabric", "ankara", "cosmetics", "electronics"]);

        yield return Child("fashion-clothing", "Fashion & Clothing", "retail-shopping", 10,
            keywords: ["tailor", "ankara", "kente", "boutique", "alterations"]);

        yield return Child("general-store", "General Store", "retail-shopping", 20);

        yield return Root("home-services", "Home Services", "🏠", 90,
            aliases: ["Home Services"],
            keywords: ["plumber", "electrician", "cleaner", "handyman", "removals"]);

        yield return Child("trades-repairs", "Trades & Repairs", "home-services", 10,
            keywords: ["plumbing", "gas safe", "boiler", "carpentry", "painting"]);

        yield return Child("cleaning", "Cleaning", "home-services", 20,
            keywords: ["domestic cleaning", "end of tenancy", "ironing"]);

        yield return Child("childcare", "Childcare", "home-services", 30,
            keywords: ["childminder", "nanny", "babysitter", "creche"]);

        yield return Root("auto-transport", "Auto & Transport", "🚗", 100,
            aliases: ["Auto & Transport", "Auto and Transport", "Transport"],
            keywords: ["taxi", "driving lessons", "mechanic", "mot", "airport transfer"]);

        yield return Child("driving-schools", "Driving Schools", "auto-transport", 10,
            keywords: ["driving instructor", "theory test", "lessons"]);

        yield return Child("mechanics", "Mechanics & Garages", "auto-transport", 20,
            keywords: ["mot", "service", "repair", "bodywork"]);

        yield return Child("taxi-transfer", "Taxi & Transfer", "auto-transport", 30,
            keywords: ["minicab", "airport run", "chauffeur"]);

        yield return Root("education", "Education", "📚", 110,
            aliases: ["Education", "Education & Student Support"],
            keywords: ["tutor", "language school", "supplementary school", "exam"]);

        yield return Child("tutoring", "Tutoring", "education", 10,
            keywords: ["maths tutor", "eleven plus", "gcse", "a level"]);

        yield return Child("language-schools", "Language Schools", "education", 20,
            keywords: ["english class", "esol", "arabic class", "french class"]);

        yield return Root("events", "Events", "🎉", 120,
            aliases: ["Events"],
            keywords: ["wedding", "photographer", "dj", "decor", "venue"]);

        yield return Child("event-planning", "Event Planning", "events", 10,
            keywords: ["wedding planner", "naming ceremony", "decor", "mc"]);

        yield return Child("photo-video", "Photography & Video", "events", 20,
            keywords: ["photographer", "videographer", "drone", "portrait"]);

        yield return Child("entertainment", "Entertainment", "events", 30,
            keywords: ["dj", "live band", "comedian", "drummer"]);

        yield return Root("it-tech", "IT & Tech", "💻", 130,
            aliases: ["IT & Tech", "IT and Tech", "Technology & Networking", "Tech"],
            keywords: ["web design", "phone repair", "software", "it support"]);

        yield return Child("web-software", "Web & Software", "it-tech", 10,
            keywords: ["website", "app", "developer", "ecommerce"]);

        yield return Child("device-repair", "Device Repair", "it-tech", 20,
            keywords: ["phone repair", "screen", "laptop repair", "unlocking"]);

        yield return Root("other", "Other", "📌", 999,
            aliases: ["Other"],
            keywords: []);
    }

    private static ServiceCategorySeed Root(
        string slug,
        string name,
        string icon,
        int sortOrder,
        IReadOnlyList<string>? aliases = null,
        IReadOnlyList<string>? keywords = null) =>
        new()
        {
            Slug = slug,
            Name = name,
            Icon = icon,
            SortOrder = sortOrder,
            Aliases = aliases ?? [],
            Keywords = keywords ?? []
        };

    private static ServiceCategorySeed Child(
        string slug,
        string name,
        string parentSlug,
        int sortOrder,
        IReadOnlyList<string>? aliases = null,
        IReadOnlyList<string>? keywords = null) =>
        new()
        {
            Slug = slug,
            Name = name,
            ParentSlug = parentSlug,
            SortOrder = sortOrder,
            Aliases = aliases ?? [],
            Keywords = keywords ?? []
        };
}
