namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// One résumé version.
/// <para>
/// The file metadata is what Media reported at upload, kept so a download button can render
/// without a round trip into B5. The bytes are never here — <see cref="MediaId"/> is the only
/// thing that can fetch them, and the link is minted by Media against the reader.
/// </para>
/// </summary>
public sealed record ResumeResponse
{
    public required Guid ResumeId { get; init; }

    public required Guid MediaId { get; init; }

    public required string Label { get; init; }

    public string? FileName { get; init; }

    public long? SizeInBytes { get; init; }

    public string? ContentType { get; init; }

    /// <summary>The version attached when the candidate does not choose one.</summary>
    public required bool IsDefault { get; init; }

    /// <summary>How many applications were sent with it.</summary>
    public required int UseCount { get; init; }

    public required DateTimeOffset UploadedAt { get; init; }
}
