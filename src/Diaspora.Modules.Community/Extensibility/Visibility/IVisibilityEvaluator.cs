namespace Diaspora.Modules.Community.Extensibility.Visibility;

/// <summary>
/// Decides whether a reader may see a piece of content.
/// <para>
/// The seam that lets a later module add an audience rule B6 never modelled — a paid-tier
/// audience, an employer-only audience, a members-of-this-organization audience — without
/// touching this one.
/// </para>
/// <para>
/// <b>An evaluator can only narrow.</b> It returns deny or abstain; there is no way for it to
/// say "allow this despite everything else", because a seam that could widen an audience would
/// make every contributed evaluator a security review.
/// </para>
/// </summary>
public interface IVisibilityEvaluator
{
    /// <summary>Lower runs first. Only matters for which reason is recorded, not for the outcome.</summary>
    int Order => 0;

    VisibilityDecision Evaluate(VisibilityRequest request);
}
