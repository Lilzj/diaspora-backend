using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.ValueObjects;

/// <summary>
/// The counters a card renders, kept on the listing itself.
/// <para>
/// <b>Denormalised on purpose, and deliberately not the analytics rollup.</b> These seven numbers
/// are read on every card in every result set, so they have to be on the row the card already
/// loaded; the rollup behind <c>ListingAnalytics</c> answers the questions a seller's dashboard
/// asks — trends, conversion, time to sale — and is read once per dashboard rather than twenty
/// times per page.
/// </para>
/// <para>
/// <b>Every mutation here is deliberately not a revision bump and deliberately not an update
/// event.</b> A buyer looking at a listing is not an edit, and it must never make a seller's
/// in-flight save fail with a conflict they cannot explain, nor fire a "listing updated" contract
/// that re-indexes the whole platform every time somebody scrolls.
/// </para>
/// </summary>
public sealed class ListingEngagement : ValueObject
{
    private ListingEngagement(
        long viewCount,
        long uniqueViewCount,
        long saveCount,
        long shareCount,
        long offerCount,
        long reservationCount,
        long searchImpressions)
    {
        ViewCount = viewCount;
        UniqueViewCount = uniqueViewCount;
        SaveCount = saveCount;
        ShareCount = shareCount;
        OfferCount = offerCount;
        ReservationCount = reservationCount;
        SearchImpressions = searchImpressions;
    }

    private ListingEngagement() { }

    public long ViewCount { get; private set; }

    /// <summary>Distinct viewers, as the view tracker de-duplicated them.</summary>
    public long UniqueViewCount { get; private set; }

    public long SaveCount { get; private set; }

    public long ShareCount { get; private set; }

    /// <summary>Negotiations opened. The brief's offer rate is this over unique views.</summary>
    public long OfferCount { get; private set; }

    public long ReservationCount { get; private set; }

    /// <summary>Times the listing appeared in a result set.</summary>
    public long SearchImpressions { get; private set; }

    /// <summary>A fresh listing. A property rather than a cached static — see B6's owned-type note.</summary>
    public static ListingEngagement None => new(0, 0, 0, 0, 0, 0, 0);

    public ListingEngagement WithView(bool unique) =>
        new(ViewCount + 1,
            UniqueViewCount + (unique ? 1 : 0),
            SaveCount, ShareCount, OfferCount, ReservationCount, SearchImpressions);

    /// <summary>
    /// Applied as a delta rather than an increment, because unsaving decrements. Floored at
    /// zero: a count that went negative would render as nonsense on a card nobody can fix.
    /// </summary>
    public ListingEngagement WithSaveDelta(int delta) =>
        new(ViewCount, UniqueViewCount,
            Math.Max(0, SaveCount + delta),
            ShareCount, OfferCount, ReservationCount, SearchImpressions);

    public ListingEngagement WithShare() =>
        new(ViewCount, UniqueViewCount, SaveCount, ShareCount + 1,
            OfferCount, ReservationCount, SearchImpressions);

    public ListingEngagement WithOfferDelta(int delta) =>
        new(ViewCount, UniqueViewCount, SaveCount, ShareCount,
            Math.Max(0, OfferCount + delta), ReservationCount, SearchImpressions);

    public ListingEngagement WithReservationDelta(int delta) =>
        new(ViewCount, UniqueViewCount, SaveCount, ShareCount, OfferCount,
            Math.Max(0, ReservationCount + delta), SearchImpressions);

    public ListingEngagement WithImpressions(long count) =>
        new(ViewCount, UniqueViewCount, SaveCount, ShareCount, OfferCount, ReservationCount,
            SearchImpressions + Math.Max(0, count));

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ViewCount;
        yield return UniqueViewCount;
        yield return SaveCount;
        yield return ShareCount;
        yield return OfferCount;
        yield return ReservationCount;
        yield return SearchImpressions;
    }
}
