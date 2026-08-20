using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>
/// Raised when a Trust decision changes an employer's trust mark.
/// <para>
/// <b>The most consequential projection in the module.</b> "Verified employer" is the badge a
/// candidate is actually reading a posting for, and a revocation has to propagate to every card
/// showing it — a stale badge on a posting from an employer Trust has just revoked is the
/// platform vouching for exactly the wrong party.
/// </para>
/// </summary>
public sealed record EmployerVerificationChangedDomainEvent : DomainEvent
{
    public required EmployerProfileId EmployerId { get; init; }

    public required JobVerificationState PreviousState { get; init; }

    public required JobVerificationState CurrentState { get; init; }

    public Guid? VerificationId { get; init; }

    public string? Badge { get; init; }

    public string? Reason { get; init; }

    /// <summary>When Trust decided, not when the projection was written.</summary>
    public required DateTimeOffset ChangedAt { get; init; }
}
