namespace Diaspora.Modules.Housing.Domain.Analytics;

/// <summary>
/// One day of a listing's measured activity.
/// <para>
/// <b>A rollup row, not an event log.</b> Housing does not store one row per view: a popular
/// hostel listing would produce tens of thousands, and nothing in the product asks a question that
/// needs them. What a landlord's dashboard asks is "how did the last fortnight go", and a day is
/// the finest resolution that question has.
/// </para>
/// <para>
/// A <see cref="DateOnly"/> rather than a timestamp, and in UTC: a day boundary that moved with
/// the reader's time zone would make two people disagree about yesterday's numbers.
/// </para>
/// </summary>
public sealed class HousingDailyMetric
{
    private HousingDailyMetric(Guid metricId, DateOnly day)
    {
        MetricId = metricId;
        Day = day;
    }

    private HousingDailyMetric() { }

    /// <summary>
    /// The row's own key, supplied by the caller - EF emits an UPDATE instead of an INSERT for a
    /// caller-keyed owned collection unless the configuration says ValueGeneratedNever.
    /// </summary>
    public Guid MetricId { get; private set; }

    public DateOnly Day { get; private set; }

    public long Views { get; private set; }

    public long UniqueViews { get; private set; }

    public long Saves { get; private set; }

    public long ViewingRequests { get; private set; }

    public long Reservations { get; private set; }

    public static HousingDailyMetric Create(Guid metricId, DateOnly day) => new(metricId, day);

    public void RecordView(bool isUnique)
    {
        Views++;

        if (isUnique)
        {
            UniqueViews++;
        }
    }

    public void RecordSave() => Saves++;

    public void RecordViewingRequest() => ViewingRequests++;

    public void RecordReservation() => Reservations++;
}
