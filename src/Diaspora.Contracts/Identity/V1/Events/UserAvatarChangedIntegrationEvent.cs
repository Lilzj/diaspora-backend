using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Identity.V1.Events;

/// <summary>
/// Published when a member sets, replaces or removes their profile photo. Modules that show the
/// member's face keep their own copy of <see cref="AvatarUrl"/> rather than asking Identity.
/// <para>
/// Both fields are null when the photo was removed. <see cref="AvatarUrl"/> is the absolute,
/// unsigned address of the photo's <c>medium</c> rendition — readable without a bearer token, so
/// it can go straight into an image tag.
/// </para>
/// </summary>
public sealed record UserAvatarChangedIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    /// <summary>The Media asset now shown as the avatar; null when it was removed.</summary>
    public Guid? MediaId { get; init; }

    /// <summary>Where anyone can load it; null when it was removed.</summary>
    public string? AvatarUrl { get; init; }
}
