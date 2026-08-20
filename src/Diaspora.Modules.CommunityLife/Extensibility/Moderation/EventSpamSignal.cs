using Diaspora.Modules.CommunityLife.Domain.Enums;

namespace Diaspora.Modules.CommunityLife.Extensibility.Moderation;

/// <summary>
/// One reason to think an event is not what it says it is.
/// <para>
/// A severity and a human-readable reason, never a number. The module composes signals from
/// providers it did not write, and two providers returning 0.7 are not commensurable — three named
/// levels can be combined honestly.
/// </para>
/// </summary>
public sealed record EventSpamSignal
{
    public required string Code { get; init; }

    public required string Reason { get; init; }

    public required EventSpamSignalSeverity Severity { get; init; }

    /// <summary>Which provider said so. Recorded in the moderation note.</summary>
    public string? Source { get; init; }
}
