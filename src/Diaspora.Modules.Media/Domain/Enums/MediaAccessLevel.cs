namespace Diaspora.Modules.Media.Domain.Enums;

/// <summary>
/// What an explicit grant permits its holder to do.
/// <para>
/// Ordered, and compared with <c>&gt;=</c>. A grantee with <see cref="Manage"/> also reads.
/// </para>
/// </summary>
public enum MediaAccessLevel
{
    /// <summary>Read the asset and its variants.</summary>
    Read = 0,

    /// <summary>Read, and change alt text, caption and visibility.</summary>
    Manage = 1,

    /// <summary>Everything, including deletion. Held by the owner and by staff.</summary>
    Own = 2
}
