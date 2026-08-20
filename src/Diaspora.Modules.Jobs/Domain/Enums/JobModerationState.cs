namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>
/// A posting's standing with moderation. Independent of its lifecycle status — a hidden
/// posting is still Open, and lifting the hide restores exactly what was there.
/// </summary>
public enum JobModerationState
{
    /// <summary>Nothing outstanding.</summary>
    Clear = 0,

    /// <summary>In the queue, still visible. Reports raise this.</summary>
    Flagged = 1,

    /// <summary>Taken off every public surface, reversibly.</summary>
    Hidden = 2
}
