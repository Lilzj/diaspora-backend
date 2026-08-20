using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Extensibility.Attributes;

namespace Diaspora.Modules.Marketplace.Infrastructure.Attributes;

/// <summary>
/// The structured fields the shipped taxonomy asks for.
/// <para>
/// <b>Deliberately sparse, and only where a filter genuinely changes the outcome.</b> A buyer
/// looking for a 128GB phone under two hundred, or a car under a hundred thousand miles, cannot get
/// that from free text; a buyer looking for a sofa does not need a "number of cushions" field, and
/// every field a seller has to fill in is a listing somebody abandons half-written.
/// </para>
/// <para>
/// <b>Nothing here is required.</b> Requiring an attribute means a seller cannot publish without it,
/// and on a peer-to-peer marketplace the person listing their old phone often genuinely does not
/// know its exact storage. A deployment that runs a stricter catalogue ships a contributor that
/// says so; the platform's default is to ask and not insist.
/// </para>
/// <para>
/// <b>Scoping is by path, so each field is declared once.</b> "Brand" sits on
/// <c>/electronics/</c> and applies to every phone, laptop and television beneath it — the whole
/// reason definitions are scoped to a subtree rather than to a leaf.
/// </para>
/// </summary>
public sealed class DefaultListingAttributeContributor : IListingAttributeContributor
{
    public string Source => "Platform";

    public IEnumerable<ListingAttributeSeed> GetAttributes()
    {
        foreach (var seed in Electronics())
        {
            yield return seed;
        }

        foreach (var seed in Vehicles())
        {
            yield return seed;
        }

        foreach (var seed in Clothing())
        {
            yield return seed;
        }

        foreach (var seed in HomeAndFurniture())
        {
            yield return seed;
        }
    }

    private static IEnumerable<ListingAttributeSeed> Electronics()
    {
        yield return new ListingAttributeSeed
        {
            Key = "brand",
            Label = "Brand",
            CategoryPath = "electronics",
            ValueKind = AttributeValueKind.Text,
            Order = 1
        };

        yield return new ListingAttributeSeed
        {
            Key = "storage-gb",
            Label = "Storage",
            CategoryPath = "electronics/mobile-phones",
            ValueKind = AttributeValueKind.Number,
            Unit = "GB",
            Order = 2
        };

        yield return new ListingAttributeSeed
        {
            Key = "colour",
            Label = "Colour",
            CategoryPath = "electronics",
            ValueKind = AttributeValueKind.Text,
            Order = 3
        };

        // The single most consequential attribute on a second-hand phone in a diaspora
        // marketplace: a locked handset bought across a border is one the buyer cannot use.
        yield return new ListingAttributeSeed
        {
            Key = "network-locked",
            Label = "Network locked",
            CategoryPath = "electronics/mobile-phones",
            ValueKind = AttributeValueKind.Boolean,
            Order = 4
        };

        yield return new ListingAttributeSeed
        {
            Key = "screen-size",
            Label = "Screen size",
            CategoryPath = "electronics",
            ValueKind = AttributeValueKind.Number,
            Unit = "in",
            Order = 5
        };

        yield return new ListingAttributeSeed
        {
            Key = "warranty",
            Label = "Warranty remaining",
            CategoryPath = "electronics",
            ValueKind = AttributeValueKind.Text,
            IsFilterable = false,
            Order = 6
        };
    }

    private static IEnumerable<ListingAttributeSeed> Vehicles()
    {
        yield return new ListingAttributeSeed
        {
            Key = "mileage",
            Label = "Mileage",
            CategoryPath = "vehicles",
            ValueKind = AttributeValueKind.Number,
            Unit = "km",
            Order = 1
        };

        yield return new ListingAttributeSeed
        {
            Key = "fuel-type",
            Label = "Fuel",
            CategoryPath = "vehicles",
            ValueKind = AttributeValueKind.Choice,
            Order = 2,
            Choices =
            [
                new ListingAttributeChoiceSeed { Value = "petrol", Label = "Petrol" },
                new ListingAttributeChoiceSeed { Value = "diesel", Label = "Diesel" },
                new ListingAttributeChoiceSeed { Value = "hybrid", Label = "Hybrid" },
                new ListingAttributeChoiceSeed { Value = "electric", Label = "Electric" },
                new ListingAttributeChoiceSeed { Value = "lpg", Label = "LPG" }
            ]
        };

        yield return new ListingAttributeSeed
        {
            Key = "transmission",
            Label = "Transmission",
            CategoryPath = "vehicles",
            ValueKind = AttributeValueKind.Choice,
            Order = 3,
            Choices =
            [
                new ListingAttributeChoiceSeed { Value = "manual", Label = "Manual" },
                new ListingAttributeChoiceSeed { Value = "automatic", Label = "Automatic" }
            ]
        };

        yield return new ListingAttributeSeed
        {
            Key = "engine-size",
            Label = "Engine size",
            CategoryPath = "vehicles",
            ValueKind = AttributeValueKind.Number,
            Unit = "cc",
            Order = 4
        };

        yield return new ListingAttributeSeed
        {
            Key = "body-type",
            Label = "Body",
            CategoryPath = "vehicles/cars",
            ValueKind = AttributeValueKind.Text,
            Order = 5
        };
    }

    private static IEnumerable<ListingAttributeSeed> Clothing()
    {
        // Free text rather than a choice list, on purpose. Sizing is not comparable across
        // countries — a UK 10 is not a US 10 is not an EU 38 — and a fixed list would force
        // sellers to pick the wrong one. The unit label carries the system the seller meant.
        yield return new ListingAttributeSeed
        {
            Key = "size",
            Label = "Size",
            CategoryPath = "clothing",
            ValueKind = AttributeValueKind.Text,
            Order = 1
        };

        yield return new ListingAttributeSeed
        {
            Key = "colour",
            Label = "Colour",
            CategoryPath = "clothing",
            ValueKind = AttributeValueKind.Text,
            Order = 2
        };

        yield return new ListingAttributeSeed
        {
            Key = "material",
            Label = "Material",
            CategoryPath = "clothing",
            ValueKind = AttributeValueKind.Text,
            IsFilterable = false,
            Order = 3
        };
    }

    private static IEnumerable<ListingAttributeSeed> HomeAndFurniture()
    {
        yield return new ListingAttributeSeed
        {
            Key = "colour",
            Label = "Colour",
            CategoryPath = "furniture",
            ValueKind = AttributeValueKind.Text,
            Order = 1
        };

        yield return new ListingAttributeSeed
        {
            Key = "material",
            Label = "Material",
            CategoryPath = "furniture",
            ValueKind = AttributeValueKind.Text,
            Order = 2
        };

        yield return new ListingAttributeSeed
        {
            Key = "dimensions",
            Label = "Dimensions",
            CategoryPath = "furniture",
            ValueKind = AttributeValueKind.Text,
            IsFilterable = false,
            Order = 3
        };

        yield return new ListingAttributeSeed
        {
            Key = "energy-rating",
            Label = "Energy rating",
            CategoryPath = "home-appliances",
            ValueKind = AttributeValueKind.Text,
            Order = 1
        };
    }
}
