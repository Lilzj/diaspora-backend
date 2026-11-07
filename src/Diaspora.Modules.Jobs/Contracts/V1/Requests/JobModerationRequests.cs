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
