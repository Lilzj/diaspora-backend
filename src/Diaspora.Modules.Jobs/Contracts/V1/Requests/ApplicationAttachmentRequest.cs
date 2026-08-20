namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>One document attached to an application. A Media reference, never a file.</summary>
public sealed record ApplicationAttachmentRequest
{
    /// <summary><c>Resume</c>, <c>CoverLetter</c>, <c>Certificate</c>, <c>Portfolio</c>, …</summary>
    public required string Kind { get; init; }

    /// <summary>B5's reference to the already-uploaded document.</summary>
    public required Guid MediaId { get; init; }

    public string? FileName { get; init; }
}
