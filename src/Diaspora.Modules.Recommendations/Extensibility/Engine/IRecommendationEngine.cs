namespace Diaspora.Modules.Recommendations.Extensibility.Engine;

/// <summary>
/// The one door into a recommendation.
/// <para>
/// <b>Every surface in the platform goes through this, and that is what makes the policies
/// unavoidable.</b> A second path — a query handler that ranked candidates itself for a special
/// case, a "quick" variant that skipped the pool — would be a path where blocked content, muted
/// categories and a personalization opt-out are not enforced. There is one implementation, an
/// architecture test asserts there is one implementation, and the reason is that the enforcement is
/// only as good as the number of ways around it.
/// </para>
/// <para>
/// <b>What the engine does not do is as important as what it does.</b> It does not decide who the
/// caller is, does not authorise, does not map to a response and does not know what a home-feed
/// section is. It takes a member and a surface and returns ranked, explained, policy-filtered
/// candidates. Everything above it composes; nothing below it ranks.
/// </para>
/// </summary>
public interface IRecommendationEngine
{
    /// <summary>
    /// Ranks a page.
    /// <para>
    /// Reuses a fresh stored set unless the request forces regeneration, and persists what it
    /// generates unless the request is transient.
    /// </para>
    /// </summary>
    Task<RecommendationResult> GenerateAsync(
        RecommendationRequest request, CancellationToken cancellationToken = default);
}
