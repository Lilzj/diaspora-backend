namespace Diaspora.Modules.Media.Domain.Enums;

/// <summary>
/// The state of an in-flight upload.
/// <para>
/// These five values are lifted verbatim from the client's own
/// <c>AttachmentUploadState</c> (<c>'queued' | 'uploading' | 'paused' | 'completed' |
/// 'failed'</c>), with <see cref="Cancelled"/> and <see cref="Expired"/> added for the two
/// endings the client has no word for. Keeping the client's vocabulary means its existing
/// reducer in <c>store/draft.ts</c> renders a real upload without a rewrite.
/// </para>
/// <para>
/// <see cref="Paused"/> is why uploads are sessions rather than a single POST: it is a state
/// the client's UI already handles, and a one-shot upload can never reach it.
/// </para>
/// </summary>
public enum UploadSessionStatus
{
    /// <summary>Initialised. No bytes received yet.</summary>
    Queued = 0,

    /// <summary>At least one chunk received, not all of them.</summary>
    Uploading = 1,

    /// <summary>Paused by the client. Chunks already received are kept.</summary>
    Paused = 2,

    /// <summary>Every chunk received and assembled into an asset.</summary>
    Completed = 3,

    /// <summary>Terminally failed — checksum mismatch, validation rejection, storage error.</summary>
    Failed = 4,

    /// <summary>Abandoned by the client.</summary>
    Cancelled = 5,

    /// <summary>Not completed inside its window. Swept, along with its parts.</summary>
    Expired = 6
}
