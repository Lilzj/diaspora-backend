using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>
/// Raised when someone joins an employer, leaves it, or changes role.
/// <para>
/// One event for all three, because the only consumer that matters — anything caching who may
/// act on whose postings — has to invalidate identically for each. <see cref="CurrentRole"/>
/// null means the member left.
/// </para>
/// </summary>
public sealed record EmployerMembershipChangedDomainEvent : DomainEvent
{
    public required EmployerProfileId EmployerId { get; init; }

    public required UserId MemberId { get; init; }

    /// <summary>Null when the member has just joined.</summary>
    public EmployerMemberRole? PreviousRole { get; init; }

    /// <summary>Null when the member has left.</summary>
    public EmployerMemberRole? CurrentRole { get; init; }

    public UserId? ChangedBy { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
