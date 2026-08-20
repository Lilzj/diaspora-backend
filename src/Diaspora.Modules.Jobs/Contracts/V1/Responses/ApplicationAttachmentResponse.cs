namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>One document sent with an application. A Media reference, never a link.</summary>
public sealed record ApplicationAttachmentResponse
{
    public required Guid AttachmentId { get; init; }

    /// <summary><c>Resume</c>, <c>CoverLetter</c>, <c>Certificate</c>, …</summary>
    public required string Kind { get; init; }

    /// <summary>Ask Media for the download link; it is minted against the reader.</summary>
    public required Guid MediaId { get; init; }

    public string? FileName { get; init; }
}
