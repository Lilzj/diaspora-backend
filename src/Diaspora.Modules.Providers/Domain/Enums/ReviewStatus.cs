namespace Diaspora.Modules.Providers.Domain.Enums;

/// <summary>
/// Whether a review counts.
/// <para>
/// The states exist because <c>ProviderReviewSummary</c> is a denormalised rollup, and a
/// summary that silently keeps counting a removed review is the failure mode that makes people
/// distrust ratings. Only <see cref="Published"/> reviews contribute to the average, the total
/// and the distribution; every transition out of it recomputes.
/// </para>
/// <para>
/// <see cref="Hidden"/> and <see cref="Removed"/> are kept apart because they are reversible on
/// different terms: a hidden review can be restored by the moderator who hid it, a removed one
/// is gone and its author has been told. Merging them would make an appeal impossible to
/// action.
/// </para>
/// </summary>
public enum ReviewStatus
{
    /// <summary>Visible, and counted in the summary.</summary>
    Published = 1,

    /// <summary>Held for review before publication. Not counted.</summary>
    PendingReview = 2,

    /// <summary>Hidden by a moderator, reversibly. Not counted.</summary>
    Hidden = 3,

    /// <summary>Removed by a moderator for cause. Not counted, not restorable in place.</summary>
    Removed = 4,

    /// <summary>Withdrawn by its own author. Not counted.</summary>
    Withdrawn = 5
}
