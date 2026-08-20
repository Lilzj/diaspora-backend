using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Events;

/// <summary>
/// A paid placement stopped.
/// <para>
/// The withdrawal half, and it matters more than the grant: a featured rail that only ever gains
/// entries ends up showing nothing but promotions that finished last month. <see cref="Status"/>
/// distinguishes lapsed from cancelled from refunded, which are three different things to anything
/// measuring what a placement was worth.
/// </para>
/// </summary>
public sealed record PlacementEndedDomainEvent : DomainEvent
{
    public required PlacementId PlacementId { get; init; }

    public required MonetizationSubjectType SubjectType { get; init; }

    public required Guid SubjectId { get; init; }

    public required PlacementKind Kind { get; init; }

    public required PlacementStatus Status { get; init; }

    public required Guid PurchasedByUserId { get; init; }

    public required DateTimeOffset EndedAt { get; init; }
}
