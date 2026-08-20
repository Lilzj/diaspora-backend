using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Series;

/// <summary>
/// Sets up a repeating event.
/// <para>
/// <b>Transactional, and this is the write in B12 where that matters most.</b> One request writes
/// the series, its first several months of occurrences and an analytics row for each; a partial
/// commit would leave a pattern whose dates were half-written and whose horizon claimed otherwise,
/// and the sweep would have no way to tell the difference between that and a series it had not
/// reached yet.
/// </para>
/// <para>
/// Gated on <c>event:create</c> rather than on a permission of its own. A series is a generator of
/// events, and somebody who may create events may create them on a pattern; inventing a second
/// permission would mean every deployment had to grant two things to allow one.
/// </para>
/// </summary>
[Authorize(Permission = CommunityLifePermissions.EventCreate)]
[Transactional(Reason = "Writes the series, its first occurrences and their analytics records.")]
[Auditable]
public sealed record CreateEventSeriesCommand : ICommand<EventSeriesResponse>
{
    public required CreateEventSeriesRequest Request { get; init; }
}
