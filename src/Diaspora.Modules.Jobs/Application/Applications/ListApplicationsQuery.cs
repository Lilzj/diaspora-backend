using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Jobs.Application.Applications;

/// <summary>
/// Lists applications — the employer's applicant list and the candidate's own tracker, which
/// are the same query read from opposite ends.
/// <para>
/// <b>Which end is decided by what the caller supplies, not by a flag they set.</b> Naming an
/// employer scopes to that employer and requires membership of it; naming nothing scopes to the
/// caller's own applications. A caller cannot ask for somebody else's.
/// </para>
/// </summary>
[RequirePermission(JobPermissions.ApplicationsReview)]
public sealed record ListApplicationsQuery : IQuery<CursorPagedResult<ApplicationCardResponse>>
{
    /// <summary>Scopes to an employer. Requires membership. Omitted, the caller's own.</summary>
    public Guid? EmployerId { get; init; }

    /// <summary>Narrows to one posting.</summary>
    public Guid? JobId { get; init; }

    public IReadOnlyList<string>? Statuses { get; init; }

    /// <summary>Only applications still under consideration.</summary>
    public bool ActiveOnly { get; init; }

    /// <summary>The employer's pipeline stage.</summary>
    public string? StageKey { get; init; }

    /// <summary>Only applications nobody at the employer has opened. The neglect view.</summary>
    public bool UnviewedOnly { get; init; }

    /// <summary>Oldest first, for clearing a backlog.</summary>
    public bool OldestFirst { get; init; }

    public string? Cursor { get; init; }

    public int Limit { get; init; } = 20;
}
