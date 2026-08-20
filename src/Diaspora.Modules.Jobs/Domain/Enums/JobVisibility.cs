namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>
/// Who may reach a posting.
/// <para>
/// <see cref="Unlisted"/> is the confidential-search case, and it is common enough in real
/// hiring to be worth a first-class value: a role that is being filled quietly is reachable by
/// anyone holding the link but appears in no search result, no category browse and no alert.
/// </para>
/// </summary>
public enum JobVisibility
{
    /// <summary>Discoverable everywhere.</summary>
    Public = 0,

    /// <summary>Reachable by direct link only. Excluded from search, browse and alerts.</summary>
    Unlisted = 1,

    /// <summary>Visible only to the employer's own team.</summary>
    Private = 2
}
