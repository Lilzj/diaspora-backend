namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>
/// Reports a job advert.
/// <para>
/// The reasons that matter most are the two about money: <c>Scam</c> and <c>FeeRequested</c>.
/// Either one flags the posting on the first report rather than waiting for a second, because
/// recruitment fraud takes from somebody who has just arrived and has nobody to ask.
/// </para>
/// </summary>
public sealed record ReportJobPostingRequest
{
    /// <summary>
    /// <c>Scam</c>, <c>FeeRequested</c>, <c>Discriminatory</c>, <c>Misleading</c>,
    /// <c>AlreadyFilled</c>, <c>NotTheEmployer</c>, <c>Duplicate</c>, <c>Offensive</c>
    /// or <c>Other</c>.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>What happened, in the reporter's words. Read by a moderator and nobody else.</summary>
    public string? Detail { get; init; }
}

/// <summary>
/// Closes a report about a job advert.
/// <para>
/// Upholding and dismissing are recorded distinctly: an employer with three upheld reports is a
/// different proposition from one with three dismissed ones, and that difference is the whole
/// signal this queue produces.
/// </para>
/// </summary>
public sealed record ResolveJobReportRequest
{
    public required bool Upheld { get; init; }

    /// <summary>What was decided. Kept on the trail, never sent to the reporter verbatim.</summary>
    public string? Resolution { get; init; }

    /// <summary>Whether to take the advert down as part of upholding it.</summary>
    public bool HidePosting { get; init; }
}

/// <summary>Applies a moderation decision to a job advert.</summary>
public sealed record ModerateJobPostingRequest
{
    /// <summary>
    /// <c>Hide</c>, <c>Unhide</c>, <c>Flag</c> or <c>Note</c>.
    /// <para>
    /// These are the verbs the posting aggregate exposes, not the states it ends in — unlike
    /// community life, which takes the states. The two modules disagree and the console keeps
    /// them apart rather than guessing.
    /// </para>
    /// </summary>
    public required string Action { get; init; }

    /// <summary>Why. Required for everything except lifting a hide.</summary>
    public string? Reason { get; init; }
}
