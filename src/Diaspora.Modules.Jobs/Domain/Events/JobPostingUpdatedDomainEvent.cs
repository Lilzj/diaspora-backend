using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>
/// Raised when anything about a posting's content changes.
/// <para>
/// <b>One event for every kind of edit, distinguished by <see cref="ChangedFields"/>.</b> To
/// every consumer that matters — the search index, the analytics rollup, a cache — the fact is
/// identical: this posting changed, re-read it. Splitting it into a dozen events by which field
/// moved would make every consumer subscribe to a dozen things to learn one.
/// </para>
/// <para>
/// The field names are stable strings the relay passes straight through to the published
/// contract, so a consumer can cheaply ignore a change it does not care about — a salary edit
/// matters to an alert, a tag edit does not.
/// </para>
/// </summary>
public sealed record JobPostingUpdatedDomainEvent : DomainEvent
{
    public required JobPostingId PostingId { get; init; }

    public required EmployerProfileId EmployerId { get; init; }

    public UserId? ChangedBy { get; init; }

    /// <summary>Which parts moved — "title", "salary", "description", "tags", "location".</summary>
    public required IReadOnlyList<string> ChangedFields { get; init; }
}
