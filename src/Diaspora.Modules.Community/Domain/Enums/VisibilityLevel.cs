namespace Diaspora.Modules.Community.Domain.Enums;

/// <summary>
/// Who may see a piece of content.
/// <para>
/// Ordered from widest to narrowest, and <b>the order is load-bearing</b>: a visibility
/// evaluator may only ever narrow an audience, never widen it, and narrower is defined as a
/// higher ordinal. That single rule is what makes contributed evaluators safe. A badly written
/// one can hide content that should have been visible, which is a bug; it cannot expose
/// content that should have been hidden, which would be a breach.
/// </para>
/// <para>
/// <see cref="Custom"/> defers to a stored visibility rule, and is how the brief requirement
/// for future custom audiences arrives without another member here.
/// </para>
/// </summary>
public enum VisibilityLevel
{
    /// <summary>Anyone, signed in or not.</summary>
    Public = 0,

    /// <summary>Any signed-in member.</summary>
    Members = 1,

    /// <summary>Members whose identity or residency Trust has verified.</summary>
    VerifiedMembers = 2,

    /// <summary>Members whose location context matches the content country.</summary>
    Country = 3,

    /// <summary>Members whose location context matches the content neighbourhood.</summary>
    Neighborhood = 4,

    /// <summary>Members who follow the author.</summary>
    Followers = 5,

    /// <summary>A stored rule decides. The seam for audiences B6 does not model.</summary>
    Custom = 6,

    /// <summary>The author alone. A draft is always this, whatever it declares.</summary>
    Private = 7
}
