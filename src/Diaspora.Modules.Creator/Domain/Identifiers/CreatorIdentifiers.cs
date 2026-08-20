using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Creator.Domain.Identifiers;

/// <summary>
/// A member's creator record.
/// <para>
/// <b>Its own identifier rather than the member's, even though the relation is one-to-one.</b> A
/// member who has never done anything creator-shaped has no row here at all, and reusing the user
/// identifier would make "is a creator" and "is a member" the same question — which is precisely
/// the conflation <c>CreatorStatus.NotACreator</c> exists to keep apart. It also means the public
/// creator directory can serve a <c>creatorId</c> without publishing a platform user identifier.
/// </para>
/// </summary>
public sealed record CreatorId(Guid Value) : StronglyTypedId(Value)
{
    public static CreatorId New() => new(Guid.NewGuid());

    public static CreatorId From(Guid value) => new(value);
}

/// <summary>One member's application to the creator programme. Not a Trust verification.</summary>
public sealed record CreatorVerificationApplicationId(Guid Value) : StronglyTypedId(Value)
{
    public static CreatorVerificationApplicationId New() => new(Guid.NewGuid());

    public static CreatorVerificationApplicationId From(Guid value) => new(value);
}

/// <summary>A milestone the programme defines. Reference data, seeded, versioned by key.</summary>
public sealed record CreatorMilestoneId(Guid Value) : StronglyTypedId(Value)
{
    public static CreatorMilestoneId New() => new(Guid.NewGuid());

    public static CreatorMilestoneId From(Guid value) => new(value);
}

/// <summary>
/// One creator's standing against one milestone.
/// <para>
/// Its own aggregate rather than a collection on the creator: a platform with forty milestones and
/// a hundred thousand creators has four million of these, and loading them to render a profile
/// would be absurd. It is also what makes one-claim-per-milestone a unique index rather than a
/// check inside a loaded list.
/// </para>
/// </summary>
public sealed record CreatorMilestoneProgressId(Guid Value) : StronglyTypedId(Value)
{
    public static CreatorMilestoneProgressId New() => new(Guid.NewGuid());

    public static CreatorMilestoneProgressId From(Guid value) => new(value);
}

/// <summary>A challenge the programme runs.</summary>
public sealed record CreatorChallengeId(Guid Value) : StronglyTypedId(Value)
{
    public static CreatorChallengeId New() => new(Guid.NewGuid());

    public static CreatorChallengeId From(Guid value) => new(value);
}

/// <summary>One task inside a challenge. Owned by the challenge, named in the client's payload.</summary>
public sealed record CreatorChallengeTaskId(Guid Value) : StronglyTypedId(Value)
{
    public static CreatorChallengeTaskId New() => new(Guid.NewGuid());

    public static CreatorChallengeTaskId From(Guid value) => new(value);
}

/// <summary>One creator's participation in one challenge, and the same argument as the milestone.</summary>
public sealed record CreatorChallengeEnrollmentId(Guid Value) : StronglyTypedId(Value)
{
    public static CreatorChallengeEnrollmentId New() => new(Guid.NewGuid());

    public static CreatorChallengeEnrollmentId From(Guid value) => new(value);
}

/// <summary>A grant programme, with its own server-defined application form.</summary>
public sealed record CommunityGrantId(Guid Value) : StronglyTypedId(Value)
{
    public static CommunityGrantId New() => new(Guid.NewGuid());

    public static CommunityGrantId From(Guid value) => new(value);
}

/// <summary>One applicant's application to one grant.</summary>
public sealed record GrantApplicationId(Guid Value) : StronglyTypedId(Value)
{
    public static GrantApplicationId New() => new(Guid.NewGuid());

    public static GrantApplicationId From(Guid value) => new(value);
}

/// <summary>A section of the Academy.</summary>
public sealed record AcademySectionId(Guid Value) : StronglyTypedId(Value)
{
    public static AcademySectionId New() => new(Guid.NewGuid());

    public static AcademySectionId From(Guid value) => new(value);
}

/// <summary>A course.</summary>
public sealed record AcademyCourseId(Guid Value) : StronglyTypedId(Value)
{
    public static AcademyCourseId New() => new(Guid.NewGuid());

    public static AcademyCourseId From(Guid value) => new(value);
}

/// <summary>A lesson. Its own aggregate because its blocks are the bulk of the Academy's data.</summary>
public sealed record AcademyLessonId(Guid Value) : StronglyTypedId(Value)
{
    public static AcademyLessonId New() => new(Guid.NewGuid());

    public static AcademyLessonId From(Guid value) => new(value);
}

/// <summary>
/// A quiz.
/// <para>
/// <b>Separate from the lesson on purpose, and the separation is a security boundary.</b> The
/// lesson read serves blocks; the quiz read serves questions and options with no answer key. The
/// key lives on the question rows of this aggregate and is loaded only by the grader, so a
/// projection that forgot to exclude it would have to reach into a different aggregate to leak it.
/// </para>
/// </summary>
public sealed record AcademyQuizId(Guid Value) : StronglyTypedId(Value)
{
    public static AcademyQuizId New() => new(Guid.NewGuid());

    public static AcademyQuizId From(Guid value) => new(value);
}

/// <summary>One question on a quiz.</summary>
public sealed record AcademyQuizQuestionId(Guid Value) : StronglyTypedId(Value)
{
    public static AcademyQuizQuestionId New() => new(Guid.NewGuid());

    public static AcademyQuizQuestionId From(Guid value) => new(value);
}

/// <summary>One creator's progress through one course.</summary>
public sealed record CreatorLearningProgressId(Guid Value) : StronglyTypedId(Value)
{
    public static CreatorLearningProgressId New() => new(Guid.NewGuid());

    public static CreatorLearningProgressId From(Guid value) => new(value);
}

/// <summary>An issued certificate.</summary>
public sealed record CreatorCertificateId(Guid Value) : StronglyTypedId(Value)
{
    public static CreatorCertificateId New() => new(Guid.NewGuid());

    public static CreatorCertificateId From(Guid value) => new(value);
}

/// <summary>One creator's referral identity — the code they share.</summary>
public sealed record CreatorReferralCodeId(Guid Value) : StronglyTypedId(Value)
{
    public static CreatorReferralCodeId New() => new(Guid.NewGuid());

    public static CreatorReferralCodeId From(Guid value) => new(value);
}

/// <summary>One person referred by one creator.</summary>
public sealed record CreatorReferralId(Guid Value) : StronglyTypedId(Value)
{
    public static CreatorReferralId New() => new(Guid.NewGuid());

    public static CreatorReferralId From(Guid value) => new(value);
}

/// <summary>One member's sign-up bonus.</summary>
public sealed record SignupBonusId(Guid Value) : StronglyTypedId(Value)
{
    public static SignupBonusId New() => new(Guid.NewGuid());

    public static SignupBonusId From(Guid value) => new(value);
}

/// <summary>Something points can be exchanged for. Reference data with a catalogue version.</summary>
public sealed record CreatorRewardId(Guid Value) : StronglyTypedId(Value)
{
    public static CreatorRewardId New() => new(Guid.NewGuid());

    public static CreatorRewardId From(Guid value) => new(value);
}

/// <summary>One exchange that happened.</summary>
public sealed record CreatorRewardRedemptionId(Guid Value) : StronglyTypedId(Value)
{
    public static CreatorRewardRedemptionId New() => new(Guid.NewGuid());

    public static CreatorRewardRedemptionId From(Guid value) => new(value);
}

/// <summary>A rule saying what an action pays. Reference data with a catalogue version.</summary>
public sealed record CreatorPointRuleId(Guid Value) : StronglyTypedId(Value)
{
    public static CreatorPointRuleId New() => new(Guid.NewGuid());

    public static CreatorPointRuleId From(Guid value) => new(value);
}

/// <summary>
/// One recognised action by one creator.
/// <para>
/// <b>The creator programme's own record of what it rewards, and not an analytics event.</b> An
/// entry says "this creator published a guide and the programme counts it as three"; it never says
/// how many people saw it. CREATOR-GAP-007 — views, reach, engagement — belongs to B19 and there
/// is nothing of it here. What this ledger buys is that a milestone can be weighted and capped, a
/// challenge task can be data-driven, a referral's qualification rule can be checked, and a
/// leaderboard can answer for a period rather than only for all time.
/// </para>
/// </summary>
public sealed record CreatorActivityEntryId(Guid Value) : StronglyTypedId(Value)
{
    public static CreatorActivityEntryId New() => new(Guid.NewGuid());

    public static CreatorActivityEntryId From(Guid value) => new(value);
}
