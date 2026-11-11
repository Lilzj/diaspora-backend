using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Series;

/// <summary>
/// One repeating event, by identifier or by handle.
/// <para>
/// Anonymous, like the event read it mirrors: somebody sent a link to a weekly service has not
/// signed up yet, and the visibility check inside the handler is what decides whether they may see
/// it.
/// </para>
/// </summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record GetEventSeriesQuery : IQuery<EventSeriesResponse>
{
    public Guid? SeriesId { get; init; }

    public string? Slug { get; init; }
}
