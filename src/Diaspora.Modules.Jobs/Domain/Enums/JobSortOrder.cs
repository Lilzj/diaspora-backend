namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>
/// How a result set is ordered.
/// <para>
/// <see cref="Relevance"/> is only meaningful when a search term was supplied; the shipped EF
/// engine degrades it to <see cref="Newest"/> rather than inventing a score, because a
/// fabricated ranking is worse than an honest one. A real engine implements it properly without
/// anything above <c>IJobSearchEngine</c> changing.
/// </para>
/// </summary>
public enum JobSortOrder
{
    /// <summary>Most recently published first. The default.</summary>
    Newest = 0,

    /// <summary>Best textual match first. Requires a search term.</summary>
    Relevance = 1,

    /// <summary>Highest advertised salary first. Undisclosed salaries sort last.</summary>
    SalaryHighToLow = 2,

    /// <summary>Lowest advertised salary first. Undisclosed salaries sort last.</summary>
    SalaryLowToHigh = 3,

    /// <summary>Soonest closing date first. Postings with no closing date sort last.</summary>
    ClosingSoon = 4,

    /// <summary>Most applications received first.</summary>
    MostApplied = 5,

    /// <summary>Featured placements first, then newest.</summary>
    Featured = 6,

    /// <summary>Nearest first. Requires a resolved geographic scope.</summary>
    Nearest = 7
}
