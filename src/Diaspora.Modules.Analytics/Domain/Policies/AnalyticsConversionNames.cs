using Diaspora.Modules.Analytics.Domain.Enums;

namespace Diaspora.Modules.Analytics.Domain.Policies;

/// <summary>
/// What a conversion is called for one kind of content, where the action has a name of its own.
/// <para>
/// <b>The catalogue's name is general on purpose.</b> One catalogue serves every kind of content, so
/// a conversion is "Actions taken" — and its definition lists the action for each: a purchase for a
/// marketplace listing, a viewing for a housing listing, an application for a job, an RSVP for an
/// event. Anywhere the figures are about one kind of content — an owner's dashboard, one item's
/// page, one item's row — the tile says that action's name instead. Where they mix kinds, as a
/// creator's overview does, or the content asks for nothing, as a guide does, the general name
/// stays, because no single word would be true.
/// </para>
/// <para>
/// <b>Names only.</b> The metric keys, the served definitions and the arithmetic are the
/// catalogue's and do not change with the name.
/// </para>
/// </summary>
/// <param name="Count">What the actions are called — "Sales".</param>
/// <param name="Rate">What actions per view are called — "Conversion rate".</param>
public sealed record AnalyticsConversionNames(string Count, string Rate)
{
    private static readonly AnalyticsConversionNames Sales = new("Sales", "Conversion rate");
    private static readonly AnalyticsConversionNames Viewings = new("Viewings", "Viewing rate");
    private static readonly AnalyticsConversionNames Applications = new("Applications", "Application rate");
    private static readonly AnalyticsConversionNames Rsvps = new("RSVPs", "RSVP rate");

    /// <summary>The names for one kind of content, or null where its action has no name of its own.</summary>
    public static AnalyticsConversionNames? For(AnalyticsContentType contentType) => contentType switch
    {
        AnalyticsContentType.Marketplace => Sales,
        AnalyticsContentType.Housing => Viewings,
        AnalyticsContentType.Job => Applications,
        AnalyticsContentType.Event => Rsvps,
        _ => null
    };
}
