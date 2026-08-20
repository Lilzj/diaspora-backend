using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>
/// Raised when an employer is suspended, reinstated or closed.
/// <para>
/// A suspension has to reach every one of the employer's open postings — leaving them live
/// while the employer is barred from recruiting is the exact failure mode moderation exists to
/// prevent. The handler for this is what takes them down.
/// </para>
/// </summary>
public sealed record EmployerStatusChangedDomainEvent : DomainEvent
{
    public required EmployerProfileId EmployerId { get; init; }

    public required EmployerStatus PreviousStatus { get; init; }

    public required EmployerStatus CurrentStatus { get; init; }

    public UserId? ChangedBy { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
