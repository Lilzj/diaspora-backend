namespace Diaspora.Application.Identity.Abstractions;

/// <summary>
/// Turns a member's stored avatar key into an address an image tag can load.
/// <para>
/// Identity stores the Media asset's identifier (see <see cref="AvatarKeys"/>) and knows nothing
/// about where Media serves bytes; the host, which composes both, supplies the address.
/// </para>
/// </summary>
public interface IAvatarUrlResolver
{
    /// <summary>
    /// The absolute, unsigned address of the avatar, or null when there is none or the key is not
    /// a Media asset.
    /// </summary>
    string? Resolve(string? avatarStorageKey);
}

/// <summary>How an avatar that is a Media asset is written into <c>AvatarReference.StorageKey</c>.</summary>
public static class AvatarKeys
{
    public static string ForMedia(Guid mediaId) => mediaId.ToString("D");

    /// <summary>
    /// The Media asset behind a stored key. False for an empty key, and for a free-text key the
    /// older <c>PUT /auth/profile</c> field may have stored.
    /// </summary>
    public static bool TryGetMediaId(string? avatarStorageKey, out Guid mediaId)
    {
        mediaId = Guid.Empty;

        return !string.IsNullOrWhiteSpace(avatarStorageKey)
            && Guid.TryParseExact(avatarStorageKey.Trim(), "D", out mediaId)
            && mediaId != Guid.Empty;
    }
}
