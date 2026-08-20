namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>Record a moderator's decision about a report.</summary>
public sealed record ResolveReportRequest
{
    /// <summary><c>Upheld</c>, <c>Dismissed</c>, <c>Duplicate</c>.</summary>
    public required string Outcome { get; init; }

    public string? Note { get; init; }

    /// <summary>
    /// The surviving subject, when a duplicate is upheld. A duplicate is not abuse — it is four
    /// committee members posting one festival — and upholding it should merge rather than punish.
    /// </summary>
    public Guid? MergedIntoSubjectId { get; init; }

    /// <summary>
    /// The action to take against the subject. <c>None</c>, <c>Hide</c>, <c>Suspend</c>,
    /// <c>Clear</c>. Applied by the handler against the aggregate, not by the report.
    /// </summary>
    public string? Action { get; init; }
}
