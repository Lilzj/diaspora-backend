using Diaspora.Modules.Marketplace.Extensibility.Categories;

namespace Diaspora.Modules.Marketplace.Infrastructure.Categories;

/// <summary>
/// The commerce taxonomy the platform ships with.
/// <para>
/// <b>An implementation of <see cref="IListingCategoryContributor"/> and nothing more special than
/// one.</b> It has no privileged position: a deployment serving a market this list does not fit
/// ships its own contributor and its categories appear beside these, and one that wants a different
/// taxonomy entirely can retire every node here through the catalogue API without a code change.
/// </para>
/// <para>
/// <b>The order is not alphabetical, and that is deliberate.</b> The departments diaspora members
/// actually trade in — phones, furniture, baby things, food — lead, because a category list is a
/// browse surface before it is a taxonomy, and the first six tiles decide whether somebody keeps
/// scrolling.
/// </para>
/// <para>
/// Two departments in the brief are declared and deliberately shallow. <c>Tickets</c> and
/// <c>Digital Goods</c> are future-ready: they exist so a listing can be filed correctly the day
/// somebody lists one, and neither carries subcategories because guessing at a structure nobody has
/// used yet produces a tree that has to be unpicked later. <c>Real Estate</c> is present as a single
/// node for the same reason, and is the seam a future Housing module claims rather than a taxonomy
/// this module intends to grow.
/// </para>
/// </summary>
public sealed class DefaultListingCategoryContributor : IListingCategoryContributor
{
    public string Source => "Platform";

    public IEnumerable<ListingCategorySeed> GetCategories()
    {
        foreach (var seed in Electronics())
        {
            yield return seed;
        }

        foreach (var seed in HomeAndLiving())
        {
            yield return seed;
        }

        foreach (var seed in Vehicles())
        {
            yield return seed;
        }

        foreach (var seed in PersonalAndFamily())
        {
            yield return seed;
        }

        foreach (var seed in LeisureAndOther())
        {
            yield return seed;
        }
    }

    private static IEnumerable<ListingCategorySeed> Electronics()
    {
        yield return new ListingCategorySeed
        {
            Key = "electronics",
            Name = "Electronics",
            Description = "Phones, computers, audio and everything with a plug.",
            IconKey = "device",
            Order = 10
        };

        yield return Child("mobile-phones", "Mobile Phones", "electronics", 11);
        yield return Child("computers", "Computers & Laptops", "electronics", 12);
        yield return Child("tablets", "Tablets & E-readers", "electronics", 13);
        yield return Child("audio", "Audio & Headphones", "electronics", 14);
        yield return Child("tv-video", "TV & Video", "electronics", 15);
        yield return Child("cameras", "Cameras & Photography", "electronics", 16);
        yield return Child("gaming", "Gaming & Consoles", "electronics", 17);
        yield return Child("electronics-accessories", "Chargers & Accessories", "electronics", 18);
    }

    private static IEnumerable<ListingCategorySeed> HomeAndLiving()
    {
        yield return new ListingCategorySeed
        {
            Key = "furniture",
            Name = "Furniture",
            Description = "Everything for a room, from a sofa to a shelf.",
            IconKey = "sofa",
            Order = 20
        };

        yield return Child("living-room", "Living Room", "furniture", 21);
        yield return Child("bedroom", "Bedroom", "furniture", 22);
        yield return Child("kitchen-dining", "Kitchen & Dining", "furniture", 23);
        yield return Child("office-furniture", "Desks & Office", "furniture", 24);
        yield return Child("storage", "Storage & Shelving", "furniture", 25);

        yield return new ListingCategorySeed
        {
            Key = "home-appliances",
            Name = "Home Appliances",
            Description = "Washing machines, fridges, cookers and small appliances.",
            IconKey = "appliance",
            Order = 30
        };

        yield return Child("large-appliances", "Large Appliances", "home-appliances", 31);
        yield return Child("small-appliances", "Small Appliances", "home-appliances", 32);
        yield return Child("kitchenware", "Kitchenware & Cookware", "home-appliances", 33);

        yield return new ListingCategorySeed
        {
            Key = "home-garden",
            Name = "Home & Garden",
            Description = "Tools, décor, bedding and everything outdoors.",
            IconKey = "home",
            Order = 40
        };

        yield return Child("tools-diy", "Tools & DIY", "home-garden", 41);
        yield return Child("garden", "Garden & Outdoor", "home-garden", 42);
        yield return Child("home-decor", "Décor & Textiles", "home-garden", 43);
    }

    private static IEnumerable<ListingCategorySeed> Vehicles()
    {
        yield return new ListingCategorySeed
        {
            Key = "vehicles",
            Name = "Vehicles",
            Description = "Cars, motorcycles, bicycles and parts.",
            IconKey = "car",
            Order = 50
        };

        yield return Child("cars", "Cars", "vehicles", 51);
        yield return Child("motorcycles", "Motorcycles & Scooters", "vehicles", 52);
        yield return Child("bicycles", "Bicycles", "vehicles", 53);
        yield return Child("vehicle-parts", "Parts & Accessories", "vehicles", 54);
        yield return Child("commercial-vehicles", "Vans & Commercial", "vehicles", 55);

        // A single node, on purpose. Housing is a module, not a marketplace department, and a
        // three-level property taxonomy here would be one that has to be unpicked the day it lands.
        yield return new ListingCategorySeed
        {
            Key = "real-estate",
            Name = "Property",
            Description = "Rooms, flats and land. A future Housing module will take this over.",
            IconKey = "building",
            Order = 60
        };
    }

    private static IEnumerable<ListingCategorySeed> PersonalAndFamily()
    {
        yield return new ListingCategorySeed
        {
            Key = "clothing",
            Name = "Clothing & Fashion",
            Description = "Womenswear, menswear, shoes and bags.",
            IconKey = "shirt",
            Order = 70
        };

        yield return Child("womens-clothing", "Womenswear", "clothing", 71);
        yield return Child("mens-clothing", "Menswear", "clothing", 72);
        yield return Child("shoes", "Shoes", "clothing", 73);
        yield return Child("bags-accessories", "Bags & Accessories", "clothing", 74);
        yield return Child("traditional-wear", "Traditional & Cultural Wear", "clothing", 75);

        yield return new ListingCategorySeed
        {
            Key = "beauty",
            Name = "Beauty & Personal Care",
            Description = "Hair, skincare, fragrance and grooming.",
            IconKey = "sparkle",
            Order = 80
        };

        yield return Child("hair", "Hair & Wigs", "beauty", 81);
        yield return Child("skincare", "Skincare & Cosmetics", "beauty", 82);
        yield return Child("fragrance", "Fragrance", "beauty", 83);

        yield return new ListingCategorySeed
        {
            Key = "baby-products",
            Name = "Baby & Child",
            Description = "Prams, cots, clothes and everything for small children.",
            IconKey = "baby",
            Order = 90
        };

        yield return Child("baby-gear", "Prams & Car Seats", "baby-products", 91);
        yield return Child("baby-clothing", "Baby & Children's Clothing", "baby-products", 92);
        yield return Child("nursery", "Nursery & Furniture", "baby-products", 93);

        yield return new ListingCategorySeed
        {
            Key = "toys",
            Name = "Toys & Games",
            IconKey = "toy",
            Order = 100
        };
    }

    private static IEnumerable<ListingCategorySeed> LeisureAndOther()
    {
        yield return new ListingCategorySeed
        {
            Key = "books",
            Name = "Books & Media",
            Description = "Books, textbooks, music and film.",
            IconKey = "book",
            Order = 110
        };

        yield return Child("textbooks", "Textbooks & Study", "books", 111);
        yield return Child("music-film", "Music & Film", "books", 112);

        yield return new ListingCategorySeed
        {
            Key = "sports",
            Name = "Sports & Fitness",
            Description = "Equipment, gym gear and outdoor kit.",
            IconKey = "sport",
            Order = 120
        };

        yield return new ListingCategorySeed
        {
            Key = "food",
            Name = "Food & Groceries",
            Description = "Imported staples, spices and speciality ingredients.",
            IconKey = "basket",
            Order = 130
        };

        yield return Child("imported-groceries", "Imported Groceries", "food", 131);
        yield return Child("spices-condiments", "Spices & Condiments", "food", 132);

        yield return new ListingCategorySeed
        {
            Key = "services",
            Name = "Services",
            Description = "Repairs, tuition, catering and skilled help.",
            IconKey = "wrench",
            Order = 140
        };

        // Future-ready, and deliberately shallow. See the class summary.
        yield return new ListingCategorySeed
        {
            Key = "tickets",
            Name = "Tickets & Events",
            Description = "Event and travel tickets.",
            IconKey = "ticket",
            Order = 150
        };

        yield return new ListingCategorySeed
        {
            Key = "digital-goods",
            Name = "Digital Goods",
            Description = "Software, subscriptions and downloads.",
            IconKey = "download",
            Order = 160
        };

        // Last, always. A catch-all near the top is a catch-all everything ends up in.
        yield return new ListingCategorySeed
        {
            Key = "other",
            Name = "Everything Else",
            IconKey = "dots",
            Order = 999
        };
    }

    private static ListingCategorySeed Child(
        string key, string name, string parentKey, int order) => new()
        {
            Key = key,
            Name = name,
            ParentKey = parentKey,
            Order = order
        };
}
