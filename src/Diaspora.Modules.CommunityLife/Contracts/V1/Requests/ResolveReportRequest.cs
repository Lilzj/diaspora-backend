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
    /// The action to take against the subject: <c>Clear</c>, <c>Hidden</c> or <c>Suspended</c>.
    /// Applied by the handler against the aggregate, not by the report.
    /// <para>
    /// These are the moderation states themselves, because that is what the subject ends up in —
    /// not the verbs <c>Hide</c> and <c>Suspend</c>, which this route does not accept. Omit the
    /// field, or send <c>None</c>, to close the report without touching its subject; any other
    /// word is refused rather than ignored.
    /// </para>
    /// </summary>
    public string? Action { get; init; }
}
