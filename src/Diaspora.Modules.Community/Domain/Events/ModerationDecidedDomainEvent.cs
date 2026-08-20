using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Events;

/// <summary>
/// Raised when a moderator, a rule or a review provider decides something about content.
/// <para>
/// The source is on the event rather than inferred from whether a moderator identifier is
/// present, because an appeal only ever asks one question — who decided this — and reconstructing
/// the answer from the shape of a payload is how that question stops being answerable.
/// </para>
/// </summary>
public sealed record ModerationDecidedDomainEvent : DomainEvent
{
    public required ModerationDecisionId DecisionId { get; init; }

    public required ContentReference Content { get; init; }

    public required ModerationActionType Action { get; init; }

    public required ModerationSource Source { get; init; }

    public Guid? ModeratorId { get; init; }

    public Guid? ContentAuthorId { get; init; }

    public string? Reason { get; init; }
}
