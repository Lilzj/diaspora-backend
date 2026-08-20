using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Events;

/// <summary>
/// Raised when an organization comes back — from the archive or from a soft delete.
/// <para>
/// <see cref="WasDeleted"/> distinguishes the two, because they mean different things to a
/// consumer: an undelete restores something that was gone, and an unarchive re-opens something that
/// was merely put away.
/// </para>
/// </summary>
public sealed record OrganizationRestoredDomainEvent : DomainEvent
{
    public required OrganizationId OrganizationId { get; init; }

    public required bool WasDeleted { get; init; }
}
