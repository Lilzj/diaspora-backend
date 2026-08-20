namespace Diaspora.Modules.Notifications.Domain.Enums;

/// <summary>Where an assembled digest is in its short life.</summary>
public enum DigestStatus
{
    /// <summary>Assembled and not yet handed to any channel.</summary>
    Generated = 0,

    /// <summary>Handed to at least one channel.</summary>
    Sent = 1,

    /// <summary>The member opened it.</summary>
    Opened = 2,

    /// <summary>The window closed with nothing worth summarising. Recorded rather than skipped, so a member who never receives one can be told why.</summary>
    Empty = 3
}
