using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>Raised when a Trust decision changes a posting's trust mark.</summary>
public sealed record JobPostingVerificationChangedDomainEvent : DomainEvent
{
    public required JobPostingId PostingId { get; init; }

    public required JobVerificationState PreviousState { get; init; }

    public required JobVerificationState CurrentState { get; init; }

    public Guid? VerificationId { get; init; }

    public string? Badge { get; init; }

    public string? Reason { get; init; }

    /// <summary>When Trust decided, not when the projection was written.</summary>
    public required DateTimeOffset ChangedAt { get; init; }
}
