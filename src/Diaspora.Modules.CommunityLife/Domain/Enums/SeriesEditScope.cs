namespace Diaspora.Modules.CommunityLife.Domain.Enums;

/// <summary>
/// What an edit to a series applies to.
/// <para>
/// <b>Mandatory on every series edit, and resolved by the caller rather than guessed by the
/// server.</b> "Move the Tuesday meetup to seven o'clock" means three different things — this week
/// only, from now on, or every week including the ones already held — and only the person editing
/// knows which. A server that picked one would silently move a hundred announced dates or silently
/// fail to move any.
/// </para>
/// </summary>
public enum SeriesEditScope
{
    /// <summary>
    /// One named occurrence. It becomes an exception: the rule no longer owns it, and later
    /// series-wide edits leave it alone.
    /// </summary>
    ThisOccurrence = 0,

    /// <summary>
    /// A named occurrence and everything after it. Occurrences already held are untouched, because
    /// an event that has happened cannot be moved.
    /// </summary>
    ThisAndFuture = 1,

    /// <summary>
    /// The rule itself and every occurrence still ahead. Occurrences already individually edited are
    /// skipped — the organizer who moved one meant it.
    /// </summary>
    EntireSeries = 2
}
