using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Providers;

/// <summary>
/// One item in a provider's gallery.
/// <para>
/// <b>A Media identifier and a role — nothing else.</b> ADR-005 and B5's
/// <c>MediaReference</c> make this non-negotiable, and the client is the cautionary tale: its
/// gallery is <c>string[]</c> of raw Unsplash URLs, and its viewer is navigated to with
/// <c>images={gallery.join(',')}</c>. Changing storage provider under that model means
/// rewriting every row and repairing nothing that clients already cached.
/// </para>
/// <para>
/// The <see cref="Role"/> is what lets one collection carry the brief's menus, price lists,
/// brochures, certificates and videos alongside photographs, without five parallel collections.
/// Roles come from <c>ProviderMediaRole</c>, which extends rather than replaces B5's own
/// published slugs.
/// </para>
/// <para>
/// <see cref="Position"/> is explicit rather than implied by insertion order, because a provider
/// reordering its photographs must not require deleting and re-uploading them, and because
/// insertion order is not a thing a relational database guarantees.
/// </para>
/// </summary>
public sealed class ProviderGalleryItem : Entity<ProviderGalleryItemId>
{
    public const int MaxRoleLength = 64;

    public const int MaxCaptionLength = 500;

    private ProviderGalleryItem(
        ProviderGalleryItemId id, MediaId mediaId, string role, int position, string? caption)
        : base(id)
    {
        MediaId = mediaId;
        Role = role;
        Position = position;
        Caption = caption;
    }

    private ProviderGalleryItem() { }

    /// <summary>The asset. The only thing needed to ask Media for a link.</summary>
    public MediaId MediaId { get; private set; } = default!;

    /// <summary>What the asset is to this provider: <c>gallery</c>, <c>menu</c>, <c>certificate</c>.</summary>
    public string Role { get; private set; } = Enums.ProviderMediaRole.Gallery;

    /// <summary>Order within the role.</summary>
    public int Position { get; private set; }

    public string? Caption { get; private set; }

    /// <summary>
    /// Whether Media has confirmed the claim. False until <c>ProviderMediaClaimer</c> has told
    /// Media this provider owns the asset — which matters because an unclaimed asset is swept
    /// by B5 within a day, silently, having reported every upload as a success.
    /// </summary>
    public bool IsClaimed { get; private set; }

    public static ProviderGalleryItem Create(
        ProviderGalleryItemId id,
        MediaId mediaId,
        string? role = null,
        int position = 0,
        string? caption = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(mediaId);

        return new ProviderGalleryItem(
            id,
            mediaId,
            NormalizeRole(role),
            position < 0 ? 0 : position,
            Trim(caption));
    }

    internal void SetPosition(int position) => Position = position < 0 ? 0 : position;

    public void SetCaption(string? caption) => Caption = Trim(caption);

    /// <summary>Records that Media accepted the claim, taking the asset out of the orphan pool.</summary>
    internal void MarkClaimed() => IsClaimed = true;

    /// <summary>
    /// Lower-cased and slugified, matching <c>MediaReference.Normalize</c> exactly. Two
    /// normalisations that differ in one detail produce rows that no longer join, which is the
    /// specific fragmentation B5's role vocabulary exists to prevent.
    /// </summary>
    private static string NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return Enums.ProviderMediaRole.Gallery;
        }

        var normalized = new string(role
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsAsciiLetterOrDigit(character) ? character : '-')
            .ToArray())
            .Trim('-');

        if (normalized.Length == 0)
        {
            return Enums.ProviderMediaRole.Gallery;
        }

        return normalized.Length <= MaxRoleLength ? normalized : normalized[..MaxRoleLength];
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim()[..Math.Min(value.Trim().Length, MaxCaptionLength)];
}
