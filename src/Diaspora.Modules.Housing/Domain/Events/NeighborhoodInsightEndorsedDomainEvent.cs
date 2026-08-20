using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Events;

/// <summary>
/// Raised when another member agrees with a published insight.
/// <para>
/// Endorsement is what turns one person's opinion into something the platform can rank by. It is
/// deliberately not a reaction in B6's sense: a reaction is a response to content, and this is a
/// second member attesting to a claim about a place they also live in.
/// </para>
/// </summary>
public sealed record NeighborhoodInsightEndorsedDomainEvent : DomainEvent
{
    public required NeighborhoodInsightId InsightId { get; init; }

    public required NeighborhoodProfileId ProfileId { get; init; }

    public required UserId EndorsedBy { get; init; }

    public required int TotalEndorsements { get; init; }
}
