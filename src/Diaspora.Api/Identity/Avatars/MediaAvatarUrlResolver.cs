using Diaspora.Application.Identity.Abstractions;
using Diaspora.Modules.Media.Application.Services;
using Diaspora.Modules.Media.Domain.Enums;

namespace Diaspora.Api.Identity.Avatars;

/// <summary>
/// Identity's avatar key, as Media serves it: the unsigned <c>medium</c> rendition.
/// <para>
/// Lives in the host because it is the one place that sees both. Identity stores a Media asset's
/// identifier; Media's URL factory knows the deployment's public base address, and the same route
/// the app already loads listing photographs from.
/// </para>
/// </summary>
public sealed class MediaAvatarUrlResolver : IAvatarUrlResolver
{
    private readonly MediaUrlFactory _urls;

    public MediaAvatarUrlResolver(MediaUrlFactory urls)
    {
        _urls = urls;
    }

    public string? Resolve(string? avatarStorageKey) =>
        AvatarKeys.TryGetMediaId(avatarStorageKey, out var mediaId)
            ? _urls.ForPublicVariant(mediaId, MediaVariantPurpose.Medium)
            : null;
}
