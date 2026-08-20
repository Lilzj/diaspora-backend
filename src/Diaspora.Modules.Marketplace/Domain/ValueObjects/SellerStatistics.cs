using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.ValueObjects;

/// <summary>
/// The seller's track record, as this module can honestly compute it.
/// <para>
/// <b>Counts and elapsed times, and nothing that looks like a rating.</b> A star rating is a
/// review, reviews are Community's, and a number here that a buyer read as a rating would be this
/// module quietly duplicating a capability another one owns. What Marketplace can say — and only
/// Marketplace can say — is how many things this seller has actually sold, how many reservations
/// they honoured, and how long they take to answer.
/// </para>
/// <para>
/// <b><see cref="CancellationRate"/> is the one number worth a buyer's attention, and it is
/// derived rather than stored.</b> A seller who cancels a third of their reservations is wasting
/// people's journeys, and that is invisible from a sold count alone.
/// </para>
/// <para>
/// Every counter is a delta applied by an event handler after the fact commits, so a statistic can
/// lag by a dispatch but can never disagree with the aggregate that produced it.
/// </para>
/// </summary>
public sealed class SellerStatistics : ValueObject
{
    private SellerStatistics(
        int publishedListings,
        int soldListings,
        int completedReservations,
        int cancelledReservations,
        int acceptedOffers,
        long totalResponseMinutes,
        int respondedConversations)
    {
        PublishedListings = publishedListings;
        SoldListings = soldListings;
        CompletedReservations = completedReservations;
        CancelledReservations = cancelledReservations;
        AcceptedOffers = acceptedOffers;
        TotalResponseMinutes = totalResponseMinutes;
        RespondedConversations = respondedConversations;
    }

    private SellerStatistics() { }

    public int PublishedListings { get; private set; }

    public int SoldListings { get; private set; }

    public int CompletedReservations { get; private set; }

    /// <summary>Reservations the seller called off. The number a buyer most deserves to see.</summary>
    public int CancelledReservations { get; private set; }

    public int AcceptedOffers { get; private set; }

    /// <summary>
    /// The running total the mean response time is computed from. Stored as a sum and a count
    /// rather than as an average, because an average cannot be updated without the count.
    /// </summary>
    public long TotalResponseMinutes { get; private set; }

    public int RespondedConversations { get; private set; }

    /// <summary>A seller with no history. Fresh instance per call — see B6's owned-type note.</summary>
    public static SellerStatistics None => new(0, 0, 0, 0, 0, 0, 0);

    /// <summary>Mean minutes to a first answer. Null until the seller has answered anything.</summary>
    public double? AverageResponseMinutes =>
        RespondedConversations == 0
            ? null
            : (double)TotalResponseMinutes / RespondedConversations;

    /// <summary>
    /// The share of finished reservations the seller called off. Null below five, because a
    /// seller who cancelled their only reservation is not a hundred-percent canceller.
    /// </summary>
    public double? CancellationRate
    {
        get
        {
            var finished = CompletedReservations + CancelledReservations;

            return finished < 5 ? null : (double)CancelledReservations / finished;
        }
    }

    public SellerStatistics WithPublishedDelta(int delta) =>
        new(Math.Max(0, PublishedListings + delta), SoldListings, CompletedReservations,
            CancelledReservations, AcceptedOffers, TotalResponseMinutes, RespondedConversations);

    public SellerStatistics WithSale() =>
        new(PublishedListings, SoldListings + 1, CompletedReservations, CancelledReservations,
            AcceptedOffers, TotalResponseMinutes, RespondedConversations);

    public SellerStatistics WithCompletedReservation() =>
        new(PublishedListings, SoldListings, CompletedReservations + 1, CancelledReservations,
            AcceptedOffers, TotalResponseMinutes, RespondedConversations);

    public SellerStatistics WithCancelledReservation() =>
        new(PublishedListings, SoldListings, CompletedReservations, CancelledReservations + 1,
            AcceptedOffers, TotalResponseMinutes, RespondedConversations);

    public SellerStatistics WithAcceptedOffer() =>
        new(PublishedListings, SoldListings, CompletedReservations, CancelledReservations,
            AcceptedOffers + 1, TotalResponseMinutes, RespondedConversations);

    /// <summary>Records how long one first answer took. Negative spans are dropped, not clamped.</summary>
    public SellerStatistics WithResponse(TimeSpan elapsed) =>
        elapsed < TimeSpan.Zero
            ? this
            : new SellerStatistics(
                PublishedListings, SoldListings, CompletedReservations, CancelledReservations,
                AcceptedOffers,
                TotalResponseMinutes + (long)Math.Round(elapsed.TotalMinutes),
                RespondedConversations + 1);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return PublishedListings;
        yield return SoldListings;
        yield return CompletedReservations;
        yield return CancelledReservations;
        yield return AcceptedOffers;
        yield return TotalResponseMinutes;
        yield return RespondedConversations;
    }
}
