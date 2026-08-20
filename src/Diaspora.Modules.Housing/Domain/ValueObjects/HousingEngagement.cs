using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.ValueObjects;

/// <summary>
/// How much attention a listing has had.
/// <para>
/// <b>Counters on the listing rather than rows to be aggregated</b>, because every one of these is
/// rendered on the card and the detail screen, and computing them from an event table would make a
/// results page a fan of aggregate queries. The measured record with its daily breakdown lives in
/// the analytics aggregate; this is the running total the write model carries.
/// </para>
/// <para>
/// <b>The ratio that matters is viewings to views.</b> A property with three thousand views and no
/// viewing requests is priced wrong, photographed badly, or not real — and that is a question no
/// infrastructure dashboard can ask.
/// </para>
/// </summary>
public sealed class HousingEngagement : ValueObject
{
    /// <summary>
    /// Unique views below which no conversion rate is published. Twenty: enough that the fraction
    /// means something, low enough that a quiet listing still gets one.
    /// </summary>
    private const long ConversionFloor = 20;

    private HousingEngagement(
        long viewCount,
        long uniqueViewCount,
        long saveCount,
        long viewingRequestCount,
        long reservationCount,
        long enquiryCount)
    {
        ViewCount = viewCount;
        UniqueViewCount = uniqueViewCount;
        SaveCount = saveCount;
        ViewingRequestCount = viewingRequestCount;
        ReservationCount = reservationCount;
        EnquiryCount = enquiryCount;
    }

    private HousingEngagement() { }

    public long ViewCount { get; private set; }

    /// <summary>Views de-duplicated per viewer inside the configured window.</summary>
    public long UniqueViewCount { get; private set; }

    public long SaveCount { get; private set; }

    public long ViewingRequestCount { get; private set; }

    public long ReservationCount { get; private set; }

    /// <summary>Questions asked through Community, counted here rather than stored here.</summary>
    public long EnquiryCount { get; private set; }

    /// <summary>
    /// Nothing yet. A fresh instance per access: this is an owned type, and a shared static one
    /// makes EF write nulls into a second aggregate that holds it.
    /// </summary>
    public static HousingEngagement Empty => new(0, 0, 0, 0, 0, 0);

    /// <summary>
    /// The share of viewers who asked to see it. Null below <see cref="ConversionFloor"/> unique
    /// views, because one view and one request is not a hundred per cent conversion and a client
    /// will render it as one.
    /// </summary>
    public double? ViewingConversionRate =>
        UniqueViewCount >= ConversionFloor
            ? (double)ViewingRequestCount / UniqueViewCount
            : null;

    public HousingEngagement RecordView(bool isUnique) =>
        With(viewCount: ViewCount + 1, uniqueViewCount: UniqueViewCount + (isUnique ? 1 : 0));

    public HousingEngagement RecordSave() => With(saveCount: SaveCount + 1);

    public HousingEngagement RecordUnsave() => With(saveCount: Math.Max(0, SaveCount - 1));

    public HousingEngagement RecordViewingRequest() =>
        With(viewingRequestCount: ViewingRequestCount + 1);

    public HousingEngagement RecordReservation() =>
        With(reservationCount: ReservationCount + 1);

    public HousingEngagement RecordEnquiry() => With(enquiryCount: EnquiryCount + 1);

    private HousingEngagement With(
        long? viewCount = null,
        long? uniqueViewCount = null,
        long? saveCount = null,
        long? viewingRequestCount = null,
        long? reservationCount = null,
        long? enquiryCount = null) =>
        new(viewCount ?? ViewCount,
            uniqueViewCount ?? UniqueViewCount,
            saveCount ?? SaveCount,
            viewingRequestCount ?? ViewingRequestCount,
            reservationCount ?? ReservationCount,
            enquiryCount ?? EnquiryCount);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ViewCount;
        yield return UniqueViewCount;
        yield return SaveCount;
        yield return ViewingRequestCount;
        yield return ReservationCount;
        yield return EnquiryCount;
    }
}
