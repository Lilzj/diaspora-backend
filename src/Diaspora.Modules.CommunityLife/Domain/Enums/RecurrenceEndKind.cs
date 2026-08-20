namespace Diaspora.Modules.CommunityLife.Domain.Enums;

/// <summary>
/// How a series stops.
/// <para>
/// <b><see cref="Never"/> is real and is why occurrences are materialised to a horizon rather than
/// generated in full.</b> A Sunday service has no last Sunday, and a model that demanded an end date
/// would make every organizer invent one.
/// </para>
/// </summary>
public enum RecurrenceEndKind
{
    /// <summary>It does not. Occurrences are topped up to a rolling horizon by the sweep.</summary>
    Never = 0,

    /// <summary>On a named date. The last occurrence is the last one starting on or before it.</summary>
    OnDate = 1,

    /// <summary>After a named number of occurrences, counting the first.</summary>
    AfterCount = 2
}
