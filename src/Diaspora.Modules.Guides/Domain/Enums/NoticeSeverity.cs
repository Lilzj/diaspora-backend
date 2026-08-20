namespace Diaspora.Modules.Guides.Domain.Enums;

/// <summary>
/// How much it matters that a reader sees this now.
/// <para>
/// <b>Severity is a fact about the notice, not an instruction to the Communication Platform.</b>
/// B8.2 publishes it on every notice contract and stops there; B13 decides who is told, over which
/// channel, at what hour and whether quiet hours are overridden. That split is why this enum can be
/// ordered by urgency without B8.2 ever having to know what a push notification is.
/// </para>
/// <para>
/// <b>The order is load-bearing.</b> Ranking a country's notices, deciding what leads a dashboard
/// and choosing what survives a truncated digest all compare these values, so the numbers ascend
/// with urgency and a new level must be inserted at its true position rather than appended.
/// </para>
/// </summary>
public enum NoticeSeverity
{
    /// <summary>Context. A consulate has published its new opening hours.</summary>
    Informational = 0,

    /// <summary>Worth knowing this month. A fee schedule changed.</summary>
    Low = 1,

    /// <summary>Worth knowing this week. Processing times have doubled.</summary>
    Medium = 2,

    /// <summary>Worth knowing today. A visa category closes at the end of the month.</summary>
    High = 3,

    /// <summary>Worth interrupting somebody for. An evacuation, a border closure, an outbreak.</summary>
    Critical = 4
}
