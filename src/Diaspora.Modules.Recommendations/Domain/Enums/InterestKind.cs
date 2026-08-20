namespace Diaspora.Modules.Recommendations.Domain.Enums;

/// <summary>
/// What sort of thing a profile interest names.
/// <para>
/// <b>Kept apart because they are drawn from different vocabularies and must not collide.</b> A
/// category key of <c>housing.rental</c> and a tag of <c>housing.rental</c> mean different things to
/// the modules that publish them, and a profile that merged the two would score a candidate twice
/// for one observation.
/// </para>
/// </summary>
public enum InterestKind
{
    /// <summary>A catalogue category key, from whichever vertical owns it.</summary>
    Category = 0,

    /// <summary>A free-form tag slug.</summary>
    Tag = 1,

    /// <summary>A skill key, from the Jobs vocabulary.</summary>
    Skill = 2,

    /// <summary>A language code.</summary>
    Language = 3,

    /// <summary>A target type the member engages with disproportionately.</summary>
    TargetType = 4,

    /// <summary>A search term the member used more than once.</summary>
    SearchTerm = 5
}
