using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>
/// Raised when a verification claim about a posting goes to Trust. Says nothing about the
/// outcome, which is Trust's alone.
/// </summary>
public sealed record JobPostingVerificationRequestedDomainEvent : DomainEvent
{
    public required JobPostingId PostingId { get; init; }

    public required EmployerProfileId EmployerId { get; init; }

    public required UserId RequestedBy { get; init; }

    /// <summary>Trust's case identifier, when submission returned one.</summary>
    public Guid? VerificationId { get; init; }

    public required DateTimeOffset RequestedAt { get; init; }
}
