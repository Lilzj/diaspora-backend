using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>
/// Raised when an employer's own details change.
/// <para>
/// The changed-field list matters more here than on a posting: an employer renaming itself
/// invalidates the denormalised name on every card of every posting it owns, and that is the
/// one change a consumer must act on rather than lazily re-read.
/// </para>
/// </summary>
public sealed record EmployerUpdatedDomainEvent : DomainEvent
{
    public required EmployerProfileId EmployerId { get; init; }

    public UserId? ChangedBy { get; init; }

    /// <summary>"identity", "contact", "branding", "regions", "description".</summary>
    public required IReadOnlyList<string> ChangedFields { get; init; }
}
