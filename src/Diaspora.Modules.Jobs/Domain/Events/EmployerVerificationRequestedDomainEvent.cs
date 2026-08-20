using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>Raised when an employer asks Trust to verify it. Says nothing about the outcome.</summary>
public sealed record EmployerVerificationRequestedDomainEvent : DomainEvent
{
    public required EmployerProfileId EmployerId { get; init; }

    public required UserId RequestedBy { get; init; }

    /// <summary>Trust's case identifier, when submission returned one.</summary>
    public Guid? VerificationId { get; init; }

    public required DateTimeOffset RequestedAt { get; init; }
}
