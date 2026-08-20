using Diaspora.Modules.CommunityLife.Domain.Series;
using Diaspora.Modules.CommunityLife.Domain.ValueObjects;

namespace Diaspora.Modules.CommunityLife.Application.Services;

/// <summary>
/// Fresh copies of a series' value objects, one set per occurrence.
/// <para>
/// <b>This type exists because of an EF trap that costs an afternoon every time somebody meets
/// it.</b> A value object mapped with <c>OwnsOne</c> is tracked as a child of <em>one</em> owner. Hand
/// the same instance to a second aggregate — which is the obvious way to stamp a series' title onto
/// the twenty-six events it generates — and EF does not copy it: it re-parents it. The first
/// aggregate's columns are then written as nulls, and the failure surfaces as
/// <c>NOT NULL constraint failed: community_life_events.title</c> from a <c>SaveChanges</c> nowhere
/// near the line that shared the instance.
/// </para>
/// <para>
/// <b>Every occurrence therefore gets its own copy, rebuilt through the same factories a client's
/// request would go through.</b> That is also the honest model: an occurrence's title is a copy of
/// the pattern's as at the moment it was written, not a reference to it, which is exactly why
/// editing one date's title does not edit the others'.
/// </para>
/// <para>
/// The platform has recorded this trap before — a cached <c>{ get; } = new(…)</c> singleton on an
/// owned type doing the same thing to two aggregates that shared it. Same cause, different shape.
/// </para>
/// </summary>
internal static class EventSeriesTemplate
{
    /// <summary>The series' title, as a new instance.</summary>
    public static CommunityLifeTitle Title(EventSeries series)
    {
        ArgumentNullException.ThrowIfNull(series);

        return CommunityLifeTitle.Create(series.Title.Value).Value;
    }

    public static CommunityLifeDescription Description(EventSeries series)
    {
        ArgumentNullException.ThrowIfNull(series);

        return CommunityLifeDescription
            .Create(series.Description.Summary, series.Description.Body)
            .Value;
    }

    /// <summary>
    /// The series' place, as a new instance.
    /// <para>
    /// Rebuilt rather than copied field by field so the neighbourhood, its path and the country B4
    /// resolved all travel together — the three are set as one act on the original and separating
    /// them here would be a second place for them to disagree.
    /// </para>
    /// </summary>
    public static CommunityLifeLocationContext Location(EventSeries series)
    {
        ArgumentNullException.ThrowIfNull(series);

        var source = series.Location;

        var context = CommunityLifeLocationContext
            .Create(source.CountryCode, source.RegionName, source.CityName)
            .Value;

        return source.NeighborhoodAreaId is { } areaId
            ? context.WithNeighborhood(
                areaId, source.AreaPath, source.NeighborhoodName, source.CountryCode)
            : context;
    }

    public static EventVenue Venue(EventSeries series)
    {
        ArgumentNullException.ThrowIfNull(series);

        var source = series.Venue;

        return EventVenue.Create(
            source.Name,
            source.AddressLine1,
            source.AddressLine2,
            source.Landmark,
            source.PostalCode,
            source.Latitude,
            source.Longitude,
            source.OnlineUrl).Value;
    }

    public static EventAudience Audience(EventSeries series)
    {
        ArgumentNullException.ThrowIfNull(series);

        var source = series.Audience;

        return EventAudience.Create(
            source.IsFamilyFriendly,
            source.IsChildFriendly,
            source.IsStudentFocused,
            source.IsFree,
            source.DressCode,
            source.AccessibilityNotes).Value;
    }

    public static ContactDetails Contact(EventSeries series)
    {
        ArgumentNullException.ThrowIfNull(series);

        var source = series.Contact;

        return ContactDetails.Create(
            source.Phone,
            source.WhatsApp,
            source.Email,
            source.Website,
            source.ContactName).Value;
    }
}
