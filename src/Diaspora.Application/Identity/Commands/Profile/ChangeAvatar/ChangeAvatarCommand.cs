using Diaspora.Application.Abstractions;

namespace Diaspora.Application.Identity.Commands.Profile.ChangeAvatar;

/// <summary>
/// Sets or removes the signed-in member's profile photo, and nothing else.
/// <para>
/// <c>PUT /auth/profile</c> can also set it, but requires every profile field on every call — too
/// heavy for a photo change, and it takes a free-text key nobody validates. This takes a Media
/// asset the host has already confirmed is the caller's own image and made public.
/// </para>
/// </summary>
[Auditable(Action = "identity.user.avatar-changed")]
[RequirePermission("identity:profile:update")]
public sealed record ChangeAvatarCommand : ICommand<AvatarChange>
{
    /// <summary>The new photo's Media asset; null removes the photo.</summary>
    public Guid? MediaId { get; init; }
}

/// <summary>What changed, including what the photo was before, so the host can withdraw it.</summary>
public sealed record AvatarChange(Guid? MediaId, string? AvatarUrl, Guid? PreviousMediaId);
