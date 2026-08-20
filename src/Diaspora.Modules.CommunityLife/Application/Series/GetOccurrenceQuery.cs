using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Series;

/// <summary>
/// One date of a repeating event.
/// <para>
/// The same row <c>/events/{id}</c> serves, projected into the occurrence shape. Both routes exist
/// because a client holding an occurrence wants its series and its sequence, and a client holding an
/// event wants its agenda and its speakers — and neither should have to read a payload built for the
/// other.
/// </para>
/// </summary>
public sealed record GetOccurrenceQuery : IQuery<EventOccurrenceResponse>
{
    public required Guid OccurrenceId { get; init; }
}
