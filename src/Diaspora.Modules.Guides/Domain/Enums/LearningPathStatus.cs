namespace Diaspora.Modules.Guides.Domain.Enums;

/// <summary>
/// Where a curated learning path sits in its lifecycle.
/// <para>
/// Three states rather than a published flag, because the middle one carries a promise: a
/// <see cref="Draft"/> path is an editor rearranging steps and may be incoherent at any instant,
/// a <see cref="Published"/> path is what a newcomer is being told to follow, and an
/// <see cref="Archived"/> one was correct until the rules changed. Collapsing draft and archived
/// into "not published" would lose the distinction between never-shown and withdrawn, which is
/// the one an editor most needs when auditing a country.
/// </para>
/// </summary>
public enum LearningPathStatus
{
    /// <summary>Being arranged. Visible to curators only.</summary>
    Draft = 0,

    /// <summary>Live on the country's onboarding surface.</summary>
    Published = 1,

    /// <summary>Withdrawn. Kept, because the steps record what the advice used to be.</summary>
    Archived = 2
}
