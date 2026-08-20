using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.ValueObjects;

/// <summary>
/// How much attention an event has had.
/// <para>
/// <b>Counters on the aggregate rather than rows in a table, and deliberately not the revision.</b>
/// Recording a view must never bump the concurrency token, or a member opening an event while its
/// organizer is editing it would make the organizer's save fail — which is the failure mode B11
/// records and every module since has had to remember.
/// </para>
/// <para>
/// The share count is here and the comment count is not. Sharing is something this module observes;
/// commenting belongs to Community, and a copy kept here would be a second number that disagrees
/// with the first.
/// </para>
/// </summary>
public sealed class EventEngagement : ValueObject
{
    private EventEngagement(
        int viewCount, int uniqueViewCount, int saveCount, int shareCount, int rsvpCount)
    {
        ViewCount = viewCount;
        UniqueViewCount = uniqueViewCount;
        SaveCount = saveCount;
        ShareCount = shareCount;
        RsvpCount = rsvpCount;
    }

    private EventEngagement() { }

    public static EventEngagement Empty => new(0, 0, 0, 0, 0);

    public int ViewCount { get; private set; }

    public int UniqueViewCount { get; private set; }

    public int SaveCount { get; private set; }

    public int ShareCount { get; private set; }

    /// <summary>Every response ever made, including withdrawn ones. Not the current guest list.</summary>
    public int RsvpCount { get; private set; }

    /// <summary>
    /// Views that became a response. The number an organizer judges a listing by, and null before
    /// anybody looked.
    /// </summary>
    public double? ConversionRate =>
        UniqueViewCount == 0 ? null : (double)RsvpCount / UniqueViewCount;

    public EventEngagement RecordView(bool isUnique) =>
        new(ViewCount + 1, UniqueViewCount + (isUnique ? 1 : 0), SaveCount, ShareCount, RsvpCount);

    public EventEngagement RecordSave() =>
        new(ViewCount, UniqueViewCount, SaveCount + 1, ShareCount, RsvpCount);

    public EventEngagement RecordUnsave() =>
        new(ViewCount, UniqueViewCount, Math.Max(0, SaveCount - 1), ShareCount, RsvpCount);

    public EventEngagement RecordShare() =>
        new(ViewCount, UniqueViewCount, SaveCount, ShareCount + 1, RsvpCount);

    public EventEngagement RecordRsvp() =>
        new(ViewCount, UniqueViewCount, SaveCount, ShareCount, RsvpCount + 1);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ViewCount;
        yield return UniqueViewCount;
        yield return SaveCount;
        yield return ShareCount;
        yield return RsvpCount;
    }
}
