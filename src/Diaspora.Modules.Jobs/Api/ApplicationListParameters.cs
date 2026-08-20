using Diaspora.Modules.Jobs.Application.Applications;
using Microsoft.AspNetCore.Mvc;

namespace Diaspora.Modules.Jobs.Api;

/// <summary>
/// The query string for an application listing.
/// <para>
/// Omitting <c>employerId</c> scopes to the caller's own applications; supplying it scopes to
/// that employer and requires membership. A caller cannot ask for somebody else's.
/// </para>
/// </summary>
public sealed record ApplicationListParameters
{
    [FromQuery(Name = "employerId")]
    public Guid? EmployerId { get; init; }

    [FromQuery(Name = "jobId")]
    public Guid? JobId { get; init; }

    [FromQuery(Name = "status")]
    public string[]? Statuses { get; init; }

    // The three flags below are nullable because the minimal API binder treats a non-nullable
    // value type as a required parameter: a plain GET with no query string would 400 rather than
    // list the caller's applications. Absent means off.
    [FromQuery(Name = "active")]
    public bool? ActiveOnly { get; init; }

    [FromQuery(Name = "stage")]
    public string? StageKey { get; init; }

    /// <summary>Only applications nobody at the employer has opened.</summary>
    [FromQuery(Name = "unviewed")]
    public bool? UnviewedOnly { get; init; }

    /// <summary>Oldest first, for clearing a backlog.</summary>
    [FromQuery(Name = "oldest")]
    public bool? OldestFirst { get; init; }

    [FromQuery(Name = "cursor")]
    public string? Cursor { get; init; }

    [FromQuery(Name = "limit")]
    public int? Limit { get; init; }

    public ListApplicationsQuery ToQuery() => new()
    {
        EmployerId = EmployerId,
        JobId = JobId,
        Statuses = Statuses,
        ActiveOnly = ActiveOnly ?? false,
        StageKey = StageKey,
        UnviewedOnly = UnviewedOnly ?? false,
        OldestFirst = OldestFirst ?? false,
        Cursor = Cursor,
        Limit = Limit ?? 20
    };
}
