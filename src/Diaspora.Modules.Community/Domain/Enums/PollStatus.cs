namespace Diaspora.Modules.Community.Domain.Enums;

/// <summary>Whether a poll is still taking votes.</summary>
public enum PollStatus
{
    Open = 0,

    /// <summary>Past its closing time. Results readable, votes refused.</summary>
    Closed = 1,

    /// <summary>Closed early by its author.</summary>
    Cancelled = 2
}
