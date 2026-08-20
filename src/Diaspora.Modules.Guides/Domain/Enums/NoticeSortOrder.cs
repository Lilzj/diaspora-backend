namespace Diaspora.Modules.Guides.Domain.Enums;

/// <summary>
/// How a list of official notices is ordered.
/// <para>
/// <b><see cref="Priority"/> is the default, and it is not what a guide listing defaults to.</b> A
/// guide list defaults to newest because a knowledge base is a library and recency is the best proxy
/// for usefulness. An intelligence list is a briefing: a critical border-closure notice from Tuesday
/// belongs above an informational fee-schedule notice from this morning, every time. Severity leads,
/// then the editor's pin, then recency — and recency alone is available for anybody who genuinely
/// wants a chronology.
/// </para>
/// </summary>
public enum NoticeSortOrder
{
    /// <summary>Severity, then the editor's priority, then newest. What a briefing looks like.</summary>
    Priority = 0,

    /// <summary>Most recently published first. A chronology.</summary>
    Newest = 1,

    /// <summary>Oldest published first. For an archive being worked through.</summary>
    Oldest = 2,

    /// <summary>Soonest to take effect first. What a member planning around a change wants.</summary>
    EffectiveSoonest = 3,

    /// <summary>Soonest to expire first. The editorial queue for what needs reviewing.</summary>
    ExpiringSoonest = 4,

    /// <summary>Most read first.</summary>
    MostViewed = 5
}
