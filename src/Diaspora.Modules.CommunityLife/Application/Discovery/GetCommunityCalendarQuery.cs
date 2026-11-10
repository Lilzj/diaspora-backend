using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Discovery;

/// <summary>
/// Everything happening in a window, from every registered source.
/// <para>
/// The module's own events are one source among several. A deployment can add public holidays, a
/// consulate's opening times or a national day calendar, and they appear beside the events without
/// this module knowing what any of them are.
/// </para>
/// </summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record GetCommunityCalendarQuery : IQuery<CommunityCalendarResponse>
{
    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public string? CountryCode { get; init; }

    public Guid? NeighborhoodAreaId { get; init; }
}
