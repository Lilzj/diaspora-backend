using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>
/// Raised when a candidate's profile changes.
/// <para>
/// <b>Deliberately carries no profile content.</b> Every other update event in this module
/// names what changed and sometimes what it changed to; this one names only the fields. A
/// candidate's skills, visa status and location are exactly the data that must not be sprayed
/// across the platform's message bus, where it would be retained in outbox rows and consumer
/// logs the candidate never agreed to. A consumer that genuinely needs the profile reads it
/// through the contract, under the visibility rules.
/// </para>
/// </summary>
public sealed record CandidateProfileUpdatedDomainEvent : DomainEvent
{
    public required CandidateProfileId CandidateId { get; init; }

    public required UserId UserId { get; init; }

    /// <summary>"headline", "skills", "experience", "education", "preferences", "authorizations".</summary>
    public required IReadOnlyList<string> ChangedFields { get; init; }
}
