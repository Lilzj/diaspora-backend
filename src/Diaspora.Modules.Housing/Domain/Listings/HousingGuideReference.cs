using Diaspora.Modules.Housing.Domain.Policies;

namespace Diaspora.Modules.Housing.Domain.Listings;

/// <summary>
/// A B8 guide worth reading beside this property.
/// <para>
/// <b>The most quietly valuable thing on a housing listing.</b> "Renting in Mauritius", "Avoiding
/// housing scams", "Understanding rental agreements" - a member about to send a deposit to a
/// stranger in a country they have not arrived in yet is exactly the reader those guides were
/// written for, and the listing page is the one screen where they will actually be read.
/// </para>
/// <para>
/// <b>An identifier and a cached title.</b> The guide belongs to B8: its body, its versions, its
/// translations and its publication state are B8's. Housing stores the reference and resolves the
/// rest through B8's published directory, so a retracted guide stops rendering without B11 knowing
/// it was retracted.
/// </para>
/// </summary>
public sealed class HousingGuideReference
{
    private HousingGuideReference(Guid guideId, string? title, string? slug, int order)
    {
        GuideId = guideId;
        Title = title;
        Slug = slug;
        Order = order;
    }

    private HousingGuideReference() { }

    /// <summary>B8's identifier. The authoritative half of this reference.</summary>
    public Guid GuideId { get; private set; }

    /// <summary>Cached for rendering. B8 owns the truth.</summary>
    public string? Title { get; private set; }

    public string? Slug { get; private set; }

    public int Order { get; private set; }

    public static HousingGuideReference Create(
        Guid guideId, string? title, string? slug, int order) =>
        new(guideId,
            Clamp(title, HousingLimits.MaxDisplayName),
            Clamp(slug, HousingLimits.MaxSlug),
            order);

    private static string? Clamp(string? value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > maximum ? trimmed[..maximum] : trimmed;
    }
}
