namespace Diaspora.Modules.Media.Domain.Enums;

/// <summary>
/// Where an asset sits in its lifecycle.
/// <para>
/// The one that matters is <see cref="Quarantined"/>. An asset whose scan came back dirty is
/// not deleted — a member who uploaded an infected file needs to be told which one, and a
/// moderator needs to be able to look at the record — but it is unreadable by everyone except
/// staff, forever, unless staff release it.
/// </para>
/// </summary>
public enum MediaAssetStatus
{
    /// <summary>Bytes are in storage; validation, scanning and processing have not finished.</summary>
    Pending = 0,

    /// <summary>Being scanned or processed. Readable by the owner, not yet by anyone else.</summary>
    Processing = 1,

    /// <summary>Everything passed. The only status a public read will serve.</summary>
    Ready = 2,

    /// <summary>
    /// A scanner flagged it, or a moderator withheld it with a reason. Its bytes are served to
    /// nobody; staff read its record, and only the quarantine release brings it back.
    /// </summary>
    Quarantined = 3,

    /// <summary>Validation or processing failed terminally.</summary>
    Failed = 4,

    /// <summary>Soft-deleted. The row survives for audit; the bytes are scheduled for removal.</summary>
    Deleted = 5
}
