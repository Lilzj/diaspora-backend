using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>Raised when a member creates a professional profile.</summary>
public sealed record CandidateProfileCreatedDomainEvent : DomainEvent
{
    public required CandidateProfileId CandidateId { get; init; }

    public required UserId UserId { get; init; }

    public required string Headline { get; init; }
}
