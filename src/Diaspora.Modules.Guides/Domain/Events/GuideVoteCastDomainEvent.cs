using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>
/// Raised when a helpfulness vote is cast or changed. <see cref="PreviousKind"/> is what lets
/// the counter handler compute the right delta for a changed mind.
/// </summary>
public sealed record GuideVoteCastDomainEvent : DomainEvent
{
    public required GuideVoteId VoteId { get; init; }

    public required GuideId GuideId { get; init; }

    public required UserId VoterId { get; init; }

    public required GuideVoteKind Kind { get; init; }

    public GuideVoteKind? PreviousKind { get; init; }
}
