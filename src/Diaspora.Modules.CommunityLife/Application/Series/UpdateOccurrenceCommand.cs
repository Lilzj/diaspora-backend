using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Series;

/// <summary>
/// Moves or calls off one date of a repeating event.
/// <para>
/// Transactional: the occurrence moves and the rule gains an exception, and either without the other
/// is a defect — an exception with no move loses a date, and a move with no exception has the next
/// expansion put it straight back.
/// </para>
/// </summary>
[RequirePermission(CommunityLifePermissions.EventEditOwn)]
[Transactional(Reason = "Moves the occurrence and records the exception on its series.")]
[Auditable]
public sealed record UpdateOccurrenceCommand : ICommand<EventOccurrenceResponse>
{
    public required Guid OccurrenceId { get; init; }

    public required UpdateOccurrenceRequest Request { get; init; }
}
