namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>
/// Submits an application.
/// <para>
/// There is no draft state, deliberately: a half-finished application is something the client
/// holds, not a row an employer might trip over — and an "applied" count that included abandoned
/// drafts would mislead everyone reading it.
/// </para>
/// <para>
/// Everything on it is frozen at submission. An employer reviewing a document must be looking at
/// what they were sent, and a candidate who could swap a résumé after a rejection would make the
/// record worthless as evidence for either of them.
/// </para>
/// </summary>
public sealed record ApplyToJobRequest
{
    /// <summary>
    /// The exact résumé version to send. Omitted, the candidate's default is used; a posting
    /// that requires a résumé and a candidate with no default is refused rather than sent
    /// without one.
    /// </summary>
    public Guid? ResumeId { get; init; }

    public string? CoverLetter { get; init; }

    /// <summary>Answers to the posting's screening questions. Required ones must be present.</summary>
    public IReadOnlyList<ApplicationAnswerRequest> Answers { get; init; } = [];

    /// <summary>Media references to already-uploaded documents. Never files.</summary>
    public IReadOnlyList<ApplicationAttachmentRequest> Attachments { get; init; } = [];
}
