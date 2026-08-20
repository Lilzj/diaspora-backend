using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.ValueObjects;

/// <summary>
/// How big an organization is and how much it does.
/// <para>
/// <b>Kept on the aggregate and settled from one place</b> — the membership status handler for the
/// member counts, the event handlers for the event counts. A directory ordering ten thousand
/// organizations by size cannot compute that from a join on every request, and an organization card
/// that showed a stale number would be worse than one that showed none.
/// </para>
/// <para>
/// <see cref="PendingRequestCount"/> is what puts the badge on an administrator's dashboard, and it
/// is the number most likely to be looked at and acted on within the hour.
/// </para>
/// </summary>
public sealed class MembershipStatistics : ValueObject
{
    private MembershipStatistics(
        int memberCount,
        int pendingRequestCount,
        int administratorCount,
        int volunteerCount,
        int publishedEventCount,
        int completedEventCount)
    {
        MemberCount = memberCount;
        PendingRequestCount = pendingRequestCount;
        AdministratorCount = administratorCount;
        VolunteerCount = volunteerCount;
        PublishedEventCount = publishedEventCount;
        CompletedEventCount = completedEventCount;
    }

    private MembershipStatistics() { }

    public static MembershipStatistics Empty => new(0, 0, 0, 0, 0, 0);

    /// <summary>Active members. Requests, invitations and departures do not count.</summary>
    public int MemberCount { get; private set; }

    public int PendingRequestCount { get; private set; }

    public int AdministratorCount { get; private set; }

    public int VolunteerCount { get; private set; }

    public int PublishedEventCount { get; private set; }

    public int CompletedEventCount { get; private set; }

    public MembershipStatistics Apply(
        int members = 0,
        int pending = 0,
        int administrators = 0,
        int volunteers = 0,
        int published = 0,
        int completed = 0) =>
        new(
            Math.Max(0, MemberCount + members),
            Math.Max(0, PendingRequestCount + pending),
            Math.Max(0, AdministratorCount + administrators),
            Math.Max(0, VolunteerCount + volunteers),
            Math.Max(0, PublishedEventCount + published),
            Math.Max(0, CompletedEventCount + completed));

    /// <summary>Restates the counters from a recount. The repair path, not the normal one.</summary>
    public MembershipStatistics Reconcile(
        int members, int pending, int administrators, int volunteers) =>
        new(
            Math.Max(0, members),
            Math.Max(0, pending),
            Math.Max(0, administrators),
            Math.Max(0, volunteers),
            PublishedEventCount,
            CompletedEventCount);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MemberCount;
        yield return PendingRequestCount;
        yield return AdministratorCount;
        yield return VolunteerCount;
        yield return PublishedEventCount;
        yield return CompletedEventCount;
    }
}
