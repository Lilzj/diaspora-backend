namespace Diaspora.Contracts.Moderation.V1;

/// <summary>
/// What a staff decision does to a piece of content, mildest first (7 Nov 2026).
/// <para>
/// <b>None of these deletes, archives, closes or cancels anything.</b> Each module carries them out
/// with its own moderation state, so an owner's earned points and paid placements are not touched by
/// the decision itself; consequences come later, once the decision stands.
/// </para>
/// </summary>
public enum ModerationOutcome
{
    /// <summary>Looked at and found to be fine. Any automatic flag comes off.</summary>
    NoAction,

    /// <summary>Taken out of public view. The owner is told, can fix it and ask for a review.</summary>
    HidePendingReview,

    /// <summary>Taken down for good, unless an appeal succeeds. Needs a second staff member.</summary>
    Remove,

    /// <summary>Put back as it was.</summary>
    Restore
}
