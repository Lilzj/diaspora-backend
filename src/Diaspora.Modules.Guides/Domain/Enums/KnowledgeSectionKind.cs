namespace Diaspora.Modules.Guides.Domain.Enums;

/// <summary>
/// The shelves a knowledge category renders.
/// <para>
/// <b>An enum of the shipped sections, not a closed set of the possible ones.</b> Sections are a
/// contributor seam: a module ships an <c>IKnowledgeSectionProvider</c> with its own key and the
/// category view grows a shelf without this file changing. What this enum names is the set B8.1
/// ships and the frontend can rely on existing on every deployment, which is a different and
/// smaller promise.
/// </para>
/// <para>
/// <see cref="PopularQuestions"/> is declared and deliberately not implemented — the brief lists
/// it as future work. Naming it here rather than leaving a gap means the wire vocabulary is
/// already stable when the section arrives, and a client that switches on section kinds does not
/// need a new case at that point.
/// </para>
/// </summary>
public enum KnowledgeSectionKind
{
    /// <summary>The category's own prose and counts. Not a guide list.</summary>
    Overview = 0,

    /// <summary>What a curator put at the top for this country.</summary>
    Featured = 1,

    /// <summary>Ranked by the helpful votes readers actually cast.</summary>
    MostHelpful = 2,

    /// <summary>Ranked by last edit. The shelf that answers "is this still true".</summary>
    RecentlyUpdated = 3,

    /// <summary>Verified by Trust. The advice somebody stood behind.</summary>
    Official = 4,

    /// <summary>Written by members and not verified. The rest of the knowledge, and most of it.</summary>
    Community = 5,

    /// <summary>Where a curator says to go next. Editor-managed, never inferred.</summary>
    RecommendedNext = 6,

    /// <summary>Reserved. Declared so the wire vocabulary is stable before the section exists.</summary>
    PopularQuestions = 7
}
