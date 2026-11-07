namespace Diaspora.Modules.Media.Contracts.V1.Requests;

/// <summary>
/// A moderator's decision about a member's file. Staff only.
/// </summary>
public sealed record ModerateMediaRequest
{
    /// <summary>
    /// <c>Hide</c> withholds the file from everyone and puts it in the quarantine queue, where the
    /// release route brings it back. <c>Remove</c> deletes it, as the member's own deletion would.
    /// </summary>
    public required string Action { get; init; }

    /// <summary>Why. Required, and kept on the file's record.</summary>
    public required string Reason { get; init; }
}
