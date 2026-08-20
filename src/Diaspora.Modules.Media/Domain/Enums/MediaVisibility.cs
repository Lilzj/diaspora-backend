namespace Diaspora.Modules.Media.Domain.Enums;

/// <summary>
/// Who may read an asset.
/// <para>
/// Defaults matter more than the values. Every asset is created <see cref="Private"/> and is
/// widened only by an explicit act, because the opposite default — public until someone
/// remembers to restrict it — is how immigration documents end up indexed.
/// </para>
/// </summary>
public enum MediaVisibility
{
    /// <summary>Owner and explicit grantees only. Always served through a signed URL.</summary>
    Private = 0,

    /// <summary>Any authenticated member. Still served through the API, never a bare bucket URL.</summary>
    Authenticated = 1,

    /// <summary>Anyone, including an anonymous caller. Listing photos, avatars, guide covers.</summary>
    Public = 2
}
