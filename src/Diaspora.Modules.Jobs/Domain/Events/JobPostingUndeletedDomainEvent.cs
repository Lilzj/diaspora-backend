using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>
/// Raised when a soft-deleted posting is restored.
/// <para>
/// Distinct from a restore out of the archive, and the difference matters to a consumer: a
/// posting returning from the archive was always readable to its employer, whereas one
/// returning from deletion was gone from every index and has to be re-added rather than
/// re-ranked.
/// </para>
/// </summary>
public sealed record JobPostingUndeletedDomainEvent : DomainEvent
{
    public required JobPostingId PostingId { get; init; }

    public required EmployerProfileId EmployerId { get; init; }
}
