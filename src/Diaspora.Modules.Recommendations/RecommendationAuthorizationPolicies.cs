namespace Diaspora.Modules.Recommendations;

/// <summary>
/// The ASP.NET authorization policies this module declares.
/// <para>
/// A policy is the endpoint-level gate; the permission attribute on the command is the handler-level
/// one. Both exist because they fail at different times: the policy refuses before a body is read,
/// the attribute refuses after the request is understood and can say which resource it was about.
/// </para>
/// <para>
/// <b>Named <c>RecommendationAuthorizationPolicies</c> rather than <c>RecommendationPolicies</c>,
/// and the extra word is load-bearing.</b> This module already uses the word "policy" for the
/// domain concept that decides whether a candidate may be recommended at all — a
/// <see cref="Domain.Policies.RecommendationPolicy"/>. Two unrelated things called
/// <c>RecommendationPolicy</c> in one assembly is the kind of collision that survives review and
/// then costs an afternoon.
/// </para>
/// <para>
/// <b>Lower case since B1.4.1, and the old names still work.</b> B14 shipped these as
/// <c>Recommendations.Member</c> and siblings while every other module wrote its policies in lower
/// case; B1.4's <c>DSP5006</c> recorded the divergence as F3. <see cref="Legacy"/> holds what they
/// used to be. The permission behind each policy is untouched.
/// </para>
/// </summary>
public static class RecommendationAuthorizationPolicies
{
    public const string Member = "recommendations.member";
    public const string Curator = "recommendations.curator";
    public const string Analyst = "recommendations.analyst";

    /// <summary>
    /// The pre-B1.4.1 names, registered as aliases and authorising identically. Deprecated by
    /// construction: nothing new references one, and their deletion is its own decision rather
    /// than a side effect of the next edit to this file.
    /// </summary>
    public static class Legacy
    {
        public const string Member = "Recommendations.Member";
        public const string Curator = "Recommendations.Curator";
        public const string Analyst = "Recommendations.Analyst";
    }
}
