namespace Diaspora.Modules.Community.Domain.Enums;

/// <summary>
/// The outcome of an automated review.
/// <para>
/// <see cref="Unavailable"/> is the member that matters, and it is the same decision B5 made
/// about virus scanning: <b>no provider registered must not look like a clean result.</b> A
/// deployment with no review provider gets Unavailable on every post, which is honest, visible
/// in a metric, and impossible to mistake for a moderated platform. A fake that always answers
/// Clean would be worse than nothing, because it makes an unmoderated platform look moderated.
/// </para>
/// </summary>
public enum ContentReviewStatus
{
    /// <summary>Queued, not yet run.</summary>
    Pending = 0,

    /// <summary>Reviewed, nothing found.</summary>
    Clean = 1,

    /// <summary>Reviewed, worth a human look. Content stays up; the queue gets it.</summary>
    Flagged = 2,

    /// <summary>Reviewed and refused. Content is hidden pending a human decision.</summary>
    Rejected = 3,

    /// <summary>No provider could answer. Not a pass.</summary>
    Unavailable = 4
}
