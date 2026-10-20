namespace Diaspora.Contracts.Community.V1.Follows;

/// <summary>
/// Read access to who follows a member, for modules that cannot reference Community.
/// <para>
/// Added 20 Oct 2026 so Notifications can tell a member's followers that the member published a
/// guide. Community owns the follow graph and implements this; it lives in Diaspora.Contracts
/// because Notifications references no business module.
/// </para>
/// </summary>
public interface IMemberFollowerDirectory
{
    /// <summary>
    /// One page of the user identifiers following this member, oldest follow first so paging is
    /// stable. Muted follows are excluded: a member who muted somebody asked not to hear from them.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListFollowerPageAsync(
        Guid memberUserId, int skip, int take, CancellationToken cancellationToken = default);
}
