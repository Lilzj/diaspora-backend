namespace Diaspora.Contracts.CommunityLife.V1.Activity;

/// <summary>
/// How many events a set of members published in a window — 21 Oct 2026.
/// </summary>
/// <param name="EventCount">Events first published in the window by one of the members.</param>
/// <param name="OrganizerCount">How many of the members organised at least one of them.</param>
public sealed record MemberEventActivity(int EventCount, int OrganizerCount);

/// <summary>
/// Counts the events a set of members organised, for modules that cannot read Community Life's tables.
/// <para>
/// Added 21 Oct 2026 for a community's statistics: no event carries a community, so the honest figure
/// is "events this community's members published". Community Life implements this. Counts only,
/// never identities.
/// </para>
/// </summary>
public interface IMemberEventActivityReader
{
    Task<MemberEventActivity> CountAsync(
        IReadOnlyCollection<Guid> memberUserIds,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken = default);
}
