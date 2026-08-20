using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>
/// Raised when a posting is created, as a draft only the employer's own team can see.
/// <para>
/// Separate from <see cref="JobPostingPublishedDomainEvent"/> deliberately: a creation is worth
/// auditing and counting, and emphatically not worth indexing, alerting on or announcing — a
/// draft is not a vacancy yet, and a candidate whose alert fired on one would be applying to
/// something that does not exist. Every downstream consumer keys on publication.
/// </para>
/// </summary>
public sealed record JobPostingCreatedDomainEvent : DomainEvent
{
    public required JobPostingId PostingId { get; init; }

    public required EmployerProfileId EmployerId { get; init; }

    public required UserId CreatedBy { get; init; }

    public required string Title { get; init; }

    public required string Slug { get; init; }

    public required string CategoryKey { get; init; }

    public required string CountryCode { get; init; }
}
