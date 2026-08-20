namespace Diaspora.Modules.Media.Contracts.V1.Requests;

/// <summary>Pauses, resumes or cancels an upload.</summary>
public sealed record SetUploadStateRequest
{
    /// <summary>
    /// <c>paused</c>, <c>uploading</c> (resume) or <c>cancelled</c>.
    /// <para>
    /// <c>completed</c>, <c>failed</c> and <c>expired</c> are outcomes the server reaches, not
    /// states a client may assert — accepting them would let a client mark its own upload
    /// complete without the bytes having arrived.
    /// </para>
    /// </summary>
    public required string State { get; init; }
}
