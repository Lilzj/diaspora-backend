using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Series;

/// <summary>
/// A page of repeating events.
/// <para>
/// The organizer's inventory of the patterns they run, and an organization's page of them.
/// Deliberately not a discovery surface: people search for something on this Saturday, and that
/// search runs against the occurrences, which are ordinary events and already carry every facet.
/// </para>
/// </summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record SearchEventSeriesQuery : IQuery<EventSeriesPageResponse>
{
    public string? SearchTerm { get; init; }

    public Guid? OrganizationId { get; init; }

    public IReadOnlyList<string>? CategoryKeys { get; init; }

    public string? CountryCode { get; init; }

    public string? CityName { get; init; }

    public IReadOnlyList<string>? Statuses { get; init; }

    /// <summary>Restrict to the caller's own, drafts included. What the organizer's list asks for.</summary>
    public bool MineOnly { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
