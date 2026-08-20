using System.Globalization;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.ValueObjects;

namespace Diaspora.Modules.CommunityLife.Application.Mapping;

/// <summary>
/// The fragments both aggregates render the same way.
/// <para>
/// <b>Static, and every mapper in this module is.</b> A class with no state is a container entry
/// nobody resolves, and CA1822 in Release is what pushed the last four modules' mappers static in
/// the first place — a rule that only bites in a Release build, which is why the CI gate runs one.
/// </para>
/// </summary>
public static class CommunityLifeSharedMapper
{
    public static LocationResponse ToResponse(CommunityLifeLocationContext location)
    {
        ArgumentNullException.ThrowIfNull(location);

        return new LocationResponse
        {
            CountryCode = location.CountryCode,
            RegionName = location.RegionName,
            CityName = location.CityName,
            NeighborhoodName = location.NeighborhoodName,
            NeighborhoodAreaId = location.NeighborhoodAreaId,
            DisplayName = location.DisplayName
        };
    }

    public static ContactResponse ToResponse(ContactDetails contact)
    {
        ArgumentNullException.ThrowIfNull(contact);

        return new ContactResponse
        {
            Phone = contact.Phone,
            WhatsApp = contact.WhatsApp,
            Email = contact.Email,
            Website = contact.Website,
            ContactName = contact.ContactName
        };
    }

    public static SocialLinksResponse ToResponse(SocialLinks links)
    {
        ArgumentNullException.ThrowIfNull(links);

        return new SocialLinksResponse
        {
            Facebook = links.Facebook,
            Instagram = links.Instagram,
            Twitter = links.Twitter,
            YouTube = links.YouTube,
            LinkedIn = links.LinkedIn,
            TikTok = links.TikTok
        };
    }

    /// <summary>A local time as <c>HH:mm</c>, or null. Invariant, because the client parses it.</summary>
    public static string? ToWireTime(TimeOnly? time) =>
        time?.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Parses a wire time. Null on anything unparseable, which the caller reads as unset.</summary>
    public static TimeOnly? FromWireTime(string? value) =>
        TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;

    /// <summary>Parses an enum name, case-insensitively. Null on anything unrecognised.</summary>
    public static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : null;

    /// <summary>The facet list on a category, split for the wire.</summary>
    public static IReadOnlyList<string> SplitFacets(string? facets) =>
        string.IsNullOrWhiteSpace(facets)
            ? []
            : [.. facets.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
}
