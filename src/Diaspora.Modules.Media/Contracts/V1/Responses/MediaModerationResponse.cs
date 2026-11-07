namespace Diaspora.Modules.Media.Contracts.V1.Responses;

/// <summary>
/// What a moderation decision left behind.
/// <para>
/// <b>Deliberately not <see cref="MediaAssetResponse"/>.</b> That record carries a URL, and a file
/// that has just been taken down has nothing servable behind one.
/// </para>
/// </summary>
public sealed record MediaModerationResponse
{
    public required Guid MediaId { get; init; }

    /// <summary><c>Hide</c> or <c>Remove</c>, as applied.</summary>
    public required string Action { get; init; }

    /// <summary>The file's status afterwards: <c>quarantined</c> or <c>deleted</c>.</summary>
    public required string Status { get; init; }

    /// <summary>The reason recorded on the file.</summary>
    public required string Reason { get; init; }

    /// <summary>Who uploaded it, so the decision can be followed up with them.</summary>
    public required Guid UploadedBy { get; init; }
}
