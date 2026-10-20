using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Events;

/// <summary>
/// Raised when a member's avatar is set, replaced or removed. <see cref="AvatarStorageKey"/> is
/// null when it was removed.
/// <para>
/// Separate from <see cref="UserProfileUpdatedDomainEvent"/>, which is raised alongside it,
/// because this one carries the new value: modules that show the member's face (Creator) keep their
/// own copy and need to know what to replace it with.
/// </para>
/// </summary>
public sealed record UserAvatarChangedDomainEvent(
    UserId UserId,
    string? AvatarStorageKey,
    DateTimeOffset ChangedAt) : DomainEvent;
