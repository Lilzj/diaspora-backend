using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>
/// Raised when a posting is soft-deleted.
/// <para>
/// Raised by the aggregate rather than left to the change tracker, for the reason B6 records
/// and B8 repeats: a delete that happens only in the tracker cannot also raise an event, and
/// downstream de-indexing needs one. A deleted posting that stays in the search index is a
/// candidate applying into a void.
/// </para>
/// </summary>
public sealed record JobPostingDeletedDomainEvent : DomainEvent
{
    public required JobPostingId PostingId { get; init; }

    public required EmployerProfileId EmployerId { get; init; }

    public required DateTimeOffset DeletedAt { get; init; }
}
