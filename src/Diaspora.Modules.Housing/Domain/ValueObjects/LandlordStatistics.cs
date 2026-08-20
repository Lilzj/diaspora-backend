using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.ValueObjects;

/// <summary>
/// A landlord's track record, as the platform can honestly report it.
/// <para>
/// <b>Every figure here is something Housing observed, not something anybody claimed.</b> There is
/// no self-reported rating and no review score - reviews belong to Community, and a number a
/// landlord types about themselves is an advertisement. What this carries is what the module
/// watched happen: how many properties they have let, how often they answer a viewing request, and
/// how often somebody who arranged to view found nobody there.
/// </para>
/// <para>
/// <b><see cref="NoShowCount"/> is the one that protects members.</b> An advance-fee rental fraud
/// looks exactly like a landlord who takes viewing requests for a property that does not exist, and
/// a count of arranged viewings that nobody turned up to is the earliest signal the platform has.
/// It is shown to moderators and fed to the fraud seam; it is deliberately not rendered as a public
/// shaming number, because one missed Saturday is a burst tyre.
/// </para>
/// </summary>
public sealed class LandlordStatistics : ValueObject
{
    /// <summary>
    /// Requests below which no response rate is published. Five: enough for the fraction to mean
    /// something, low enough that a new landlord is not permanently unrated.
    /// </summary>
    private const int ResponseFloor = 5;

    private LandlordStatistics(
        int activeListings,
        int totalListings,
        int completedLets,
        int viewingRequests,
        int viewingsAnswered,
        int noShowCount,
        DateTimeOffset? lastRespondedAt)
    {
        ActiveListings = activeListings;
        TotalListings = totalListings;
        CompletedLets = completedLets;
        ViewingRequests = viewingRequests;
        ViewingsAnswered = viewingsAnswered;
        NoShowCount = noShowCount;
        LastRespondedAt = lastRespondedAt;
    }

    private LandlordStatistics() { }

    public int ActiveListings { get; private set; }

    public int TotalListings { get; private set; }

    /// <summary>Properties somebody actually moved into. The number that means the landlord is real.</summary>
    public int CompletedLets { get; private set; }

    public int ViewingRequests { get; private set; }

    /// <summary>Requests the landlord answered, either way. Declining promptly counts.</summary>
    public int ViewingsAnswered { get; private set; }

    /// <summary>Arranged viewings the landlord did not attend. See the class summary.</summary>
    public int NoShowCount { get; private set; }

    public DateTimeOffset? LastRespondedAt { get; private set; }

    /// <summary>
    /// The share of viewing requests answered. Null below <see cref="ResponseFloor"/> requests,
    /// because one ignored message is not a nought per cent response rate.
    /// </summary>
    public double? ResponseRate =>
        ViewingRequests >= ResponseFloor
            ? (double)ViewingsAnswered / ViewingRequests
            : null;

    /// <summary>
    /// Nothing yet. A fresh instance per access: this is an owned type, and a shared static one
    /// makes EF write nulls into a second aggregate holding it.
    /// </summary>
    public static LandlordStatistics Empty => new(0, 0, 0, 0, 0, 0, null);

    public LandlordStatistics WithListingCounts(int activeListings, int totalListings) =>
        With(activeListings: Math.Max(0, activeListings),
            totalListings: Math.Max(0, totalListings));

    public LandlordStatistics RecordLet() => With(completedLets: CompletedLets + 1);

    public LandlordStatistics RecordViewingRequest() =>
        With(viewingRequests: ViewingRequests + 1);

    public LandlordStatistics RecordViewingAnswered(DateTimeOffset answeredAt) =>
        With(viewingsAnswered: ViewingsAnswered + 1, lastRespondedAt: answeredAt);

    public LandlordStatistics RecordNoShow() => With(noShowCount: NoShowCount + 1);

    private LandlordStatistics With(
        int? activeListings = null,
        int? totalListings = null,
        int? completedLets = null,
        int? viewingRequests = null,
        int? viewingsAnswered = null,
        int? noShowCount = null,
        DateTimeOffset? lastRespondedAt = null) =>
        new(activeListings ?? ActiveListings,
            totalListings ?? TotalListings,
            completedLets ?? CompletedLets,
            viewingRequests ?? ViewingRequests,
            viewingsAnswered ?? ViewingsAnswered,
            noShowCount ?? NoShowCount,
            lastRespondedAt ?? LastRespondedAt);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ActiveListings;
        yield return TotalListings;
        yield return CompletedLets;
        yield return ViewingRequests;
        yield return ViewingsAnswered;
        yield return NoShowCount;
        yield return LastRespondedAt;
    }
}
