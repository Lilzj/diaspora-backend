using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Analytics;

/// <summary>
/// A listing's measured record, kept beside the listing rather than inside it.
/// <para>
/// <b>Separate from the listing for one reason: it outlives the listing's visibility.</b> A
/// landlord's dashboard is computed from properties they archived two years ago, and a
/// neighbourhood's average rent and time-to-let are computed from listings that are no longer on
/// the market. Folding this into the listing would mean either loading the measured record on
/// every card render or losing it when the property was archived.
/// </para>
/// <para>
/// <b>It is not soft-deleted, and that is deliberate.</b> The listing may be; the measurement of
/// what happened to it is a fact about the past. Deleting the analytics with the listing would
/// quietly rewrite every neighbourhood average that included it.
/// </para>
/// <para>
/// <b>The daily breakdown is bounded and rolled up.</b> Days beyond the retention window are
/// dropped and their totals stay in the running counters, so a listing that has been live for
/// three years does not carry a thousand rows to render a fourteen-day sparkline.
/// </para>
/// </summary>
public sealed class HousingAnalytics : AggregateRoot<HousingAnalyticsId>, IAuditableEntity
{
    /// <summary>
    /// How many days of breakdown are kept. Ninety: long enough to see a season, short enough that
    /// the table stays a rollup rather than an event log.
    /// </summary>
    public const int RetentionDays = 90;

    private readonly List<HousingDailyMetric> _daily = [];

    private HousingAnalytics(
        HousingAnalyticsId id,
        HousingListingId listingId,
        LandlordProfileId landlordId,
        DateTimeOffset createdAt) : base(id)
    {
        ListingId = listingId;
        LandlordId = landlordId;
        CreatedAt = createdAt;
    }

    private HousingAnalytics() { }

    public HousingListingId ListingId { get; private set; } = default!;

    public LandlordProfileId LandlordId { get; private set; } = default!;

    /// <summary>
    /// B4's area, denormalised. What makes "average time to let in this neighbourhood" a query
    /// against this table rather than a join through every listing.
    /// </summary>
    public Guid? NeighborhoodAreaId { get; private set; }

    public string CountryCode { get; private set; } = string.Empty;

    /// <summary>The accommodation type, denormalised so trends can be grouped by it.</summary>
    public string AccommodationTypeKey { get; private set; } = string.Empty;

    // ── Running totals ──────────────────────────────────────────────────────────────────────

    public long TotalViews { get; private set; }

    public long UniqueViews { get; private set; }

    public long ViewingRequests { get; private set; }

    public long ViewingsCompleted { get; private set; }

    public long Reservations { get; private set; }

    /// <summary>Reservations that ended without anybody moving in.</summary>
    public long ReservationsAbandoned { get; private set; }

    public long Occupations { get; private set; }

    public long Saves { get; private set; }

    public long Reports { get; private set; }

    // ── Outcome ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Publication to first occupation.
    /// <para>
    /// <b>The figure the analytics guide is really about.</b> A neighbourhood where the median is
    /// four days is one where a member needs to move quickly; one where it is four months is one
    /// where they can negotiate. It is stored rather than derived because the listing's publication
    /// date is mutable and this is not.
    /// </para>
    /// </summary>
    public TimeSpan? TimeToLet { get; private set; }

    /// <summary>The rent at the moment it was let, in its own currency. Never converted.</summary>
    public decimal? LetAtAmount { get; private set; }

    public string? LetAtCurrency { get; private set; }

    /// <summary>
    /// The period that rent was quoted for.
    /// <para>
    /// Stored beside the amount because it is a grouping key, not a decoration: a nightly hostel
    /// rate averaged together with a monthly flat rent produces a figure that is wrong in a way
    /// nobody reading the chart can see. Every market statistic groups on this before it averages.
    /// </para>
    /// </summary>
    public RentPeriod LetAtPeriod { get; private set; }

    public DateTimeOffset? FirstPublishedAt { get; private set; }

    public DateTimeOffset? FirstOccupiedAt { get; private set; }

    public IReadOnlyList<HousingDailyMetric> Daily =>
        _daily.OrderBy(metric => metric.Day).ToList().AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    /// <summary>
    /// The share of unique viewers who asked to see it. Null below twenty unique views, for the
    /// reason <c>HousingEngagement</c> gives: one view and one request is not a full conversion.
    /// </summary>
    public double? ViewingConversionRate =>
        UniqueViews >= 20 ? (double)ViewingRequests / UniqueViews : null;

    /// <summary>
    /// The share of reservations that ended with somebody moving in. What tells a landlord whether
    /// their asking rent survives contact with a member's budget.
    /// </summary>
    public double? ReservationCompletionRate =>
        Reservations >= 3 ? (double)Occupations / Reservations : null;

    public static HousingAnalytics Open(
        HousingAnalyticsId id,
        HousingListingId listingId,
        LandlordProfileId landlordId,
        string countryCode,
        string accommodationTypeKey,
        Guid? neighborhoodAreaId,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(listingId);
        ArgumentNullException.ThrowIfNull(landlordId);

        return new HousingAnalytics(id, listingId, landlordId, createdAt)
        {
            CountryCode = countryCode,
            AccommodationTypeKey = accommodationTypeKey,
            NeighborhoodAreaId = neighborhoodAreaId
        };
    }

    /// <summary>Keeps the denormalised grouping keys in step with the listing.</summary>
    public void Reclassify(
        string countryCode, string accommodationTypeKey, Guid? neighborhoodAreaId)
    {
        CountryCode = countryCode;
        AccommodationTypeKey = accommodationTypeKey;
        NeighborhoodAreaId = neighborhoodAreaId;
    }

    public void RecordView(DateTimeOffset occurredAt, bool isUnique)
    {
        TotalViews++;

        if (isUnique)
        {
            UniqueViews++;
        }

        Day(occurredAt).RecordView(isUnique);
        Prune(occurredAt);
    }

    public void RecordSave(DateTimeOffset occurredAt)
    {
        Saves++;
        Day(occurredAt).RecordSave();
        Prune(occurredAt);
    }

    public void RecordViewingRequest(DateTimeOffset occurredAt)
    {
        ViewingRequests++;
        Day(occurredAt).RecordViewingRequest();
        Prune(occurredAt);
    }

    public void RecordViewingCompleted(DateTimeOffset occurredAt)
    {
        ViewingsCompleted++;
        Prune(occurredAt);
    }

    public void RecordReservation(DateTimeOffset occurredAt)
    {
        Reservations++;
        Day(occurredAt).RecordReservation();
        Prune(occurredAt);
    }

    public void RecordReservationAbandoned(DateTimeOffset occurredAt)
    {
        ReservationsAbandoned++;
        Prune(occurredAt);
    }

    public void RecordReport(DateTimeOffset occurredAt)
    {
        Reports++;
        Prune(occurredAt);
    }

    /// <summary>Records the first publication. Never overwritten — see <see cref="TimeToLet"/>.</summary>
    public void RecordPublication(DateTimeOffset publishedAt) =>
        FirstPublishedAt ??= publishedAt;

    /// <summary>
    /// Records that somebody moved in, and fixes the time-to-let.
    /// <para>
    /// Only the first occupation counts. A property let three times over two years has three
    /// tenancies and one answer to "how long did it take to let when it was new", and averaging
    /// the three would make a much-relet property look slow.
    /// </para>
    /// </summary>
    public void RecordOccupation(
        DateTimeOffset occupiedAt,
        decimal? amount,
        string? currencyCode,
        RentPeriod period = RentPeriod.None)
    {
        Occupations++;

        if (FirstOccupiedAt is not null)
        {
            return;
        }

        FirstOccupiedAt = occupiedAt;
        LetAtAmount = amount;
        LetAtCurrency = currencyCode;
        LetAtPeriod = period;

        if (FirstPublishedAt is { } published && occupiedAt > published)
        {
            TimeToLet = occupiedAt - published;
        }
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    private HousingDailyMetric Day(DateTimeOffset occurredAt)
    {
        var day = DateOnly.FromDateTime(occurredAt.UtcDateTime);
        var existing = _daily.FirstOrDefault(metric => metric.Day == day);

        if (existing is not null)
        {
            return existing;
        }

        var created = HousingDailyMetric.Create(Guid.NewGuid(), day);

        _daily.Add(created);

        return created;
    }

    /// <summary>
    /// Drops days outside the retention window. The running totals already carry them, so nothing
    /// is lost except the ability to chart three years at daily resolution — which nothing does.
    /// </summary>
    private void Prune(DateTimeOffset now)
    {
        var cutoff = DateOnly.FromDateTime(now.UtcDateTime.AddDays(-RetentionDays));

        _daily.RemoveAll(metric => metric.Day < cutoff);
    }
}
