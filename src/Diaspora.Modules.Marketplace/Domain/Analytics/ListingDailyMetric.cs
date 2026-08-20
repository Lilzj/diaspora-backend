namespace Diaspora.Modules.Marketplace.Domain.Analytics;

/// <summary>
/// One day of a listing's measured life.
/// <para>
/// <b>A daily bucket rather than a row per view, and that is the entire reason the brief's
/// "ListingView" is not an aggregate.</b> A marketplace with a hundred thousand listings produces
/// millions of views a week; storing each one buys the ability to answer questions nobody asks and
/// costs a table that has to be pruned forever. A bucket per listing per day answers every
/// question the brief actually poses — trends, conversion, geographic and category movement — at a
/// thousandth of the size.
/// </para>
/// <para>
/// Owned by the rollup, because a day has no meaning apart from the listing whose day it is.
/// </para>
/// </summary>
public sealed class ListingDailyMetric
{
    private ListingDailyMetric(DateOnly day)
    {
        Day = day;
    }

    private ListingDailyMetric() { }

    public DateOnly Day { get; private set; }

    public int Views { get; private set; }

    public int UniqueViews { get; private set; }

    public int Saves { get; private set; }

    public int Shares { get; private set; }

    public int Offers { get; private set; }

    public int Reservations { get; private set; }

    public int Sales { get; private set; }

    /// <summary>Times the listing appeared in a result set that day.</summary>
    public int Impressions { get; private set; }

    public static ListingDailyMetric For(DateOnly day) => new(day);

    /// <summary>
    /// Applies one day's movements. Mutating in place rather than returning a new instance,
    /// because this is an owned entity EF tracks by key rather than a value object.
    /// </summary>
    public void Apply(
        int views = 0,
        int uniqueViews = 0,
        int saves = 0,
        int shares = 0,
        int offers = 0,
        int reservations = 0,
        int sales = 0,
        int impressions = 0)
    {
        Views = Math.Max(0, Views + views);
        UniqueViews = Math.Max(0, UniqueViews + uniqueViews);
        Saves = Math.Max(0, Saves + saves);
        Shares = Math.Max(0, Shares + shares);
        Offers = Math.Max(0, Offers + offers);
        Reservations = Math.Max(0, Reservations + reservations);
        Sales = Math.Max(0, Sales + sales);
        Impressions = Math.Max(0, Impressions + impressions);
    }
}
