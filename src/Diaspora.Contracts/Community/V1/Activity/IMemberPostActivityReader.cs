namespace Diaspora.Contracts.Community.V1.Activity;

/// <summary>
/// How much a set of members published to the social feed in a window — 21 Oct 2026.
/// </summary>
/// <param name="PostCount">Posts first published in the window and not since moderated away or deleted.</param>
/// <param name="AuthorCount">How many of the members wrote at least one of them.</param>
public sealed record MemberPostActivity(int PostCount, int AuthorCount);

/// <summary>
/// Counts the posts a set of members published, for modules that cannot read Community's tables.
/// <para>
/// Added 21 Oct 2026 for a community's statistics: no post on the platform carries a community, so
/// "activity in this community" is honestly "what this community's members published", and only
/// Community can count that. Community implements this; it lives here so the asking module needs no
/// reference to Community's internals.
/// </para>
/// <para>
/// <b>Counts only, never identities.</b> The answer cannot say who posted, so a caller cannot use it
/// to reveal a member whose membership is private.
/// </para>
/// </summary>
public interface IMemberPostActivityReader
{
    Task<MemberPostActivity> CountAsync(
        IReadOnlyCollection<Guid> memberUserIds,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken = default);
}
