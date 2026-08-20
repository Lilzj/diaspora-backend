namespace Diaspora.Application.Identity.Authorization;

/// <summary>
/// What a capability is <em>for</em>, independent of which module owns it.
/// <para>
/// Access tiers say <em>when</em> a capability opens. This says <em>why</em>. The two were
/// conflated before B2.0.1, which is how the platform came to hold two incompatible readings
/// of <c>AccessTier.Onboarding</c>: Media read it as "signed up, may do onboarding things" and
/// an Identity test read it as "not yet a member, may do nothing". Both are defensible
/// sentences; having both is not.
/// </para>
/// <para>
/// Every permission and every feature gate on the platform belongs to exactly one of these,
/// and <see cref="CapabilityClassifier"/> decides which — totally, from data the definition
/// already carries. Nothing was added to the frozen <c>PermissionDefinition</c> or
/// <c>FeatureDefinition</c> contracts to make that possible.
/// </para>
/// </summary>
public enum CapabilityCategory
{
    /// <summary>
    /// Exists so a member can <b>finish becoming a member</b>.
    /// <para>
    /// Verifying a phone number, choosing a country of origin, adding a profile photograph,
    /// submitting verification evidence, reading back the document you just uploaded. The test
    /// is not "is it small" — it is <b>would withholding this make onboarding impossible, or
    /// create a loop with no entrance?</b> Gating a profile photograph behind verification
    /// when the reputation model scores adding one is such a loop.
    /// </para>
    /// <para>
    /// <b>An onboarding capability is never a membership privilege.</b> Nothing in this
    /// category lets a subject read the community, comment, react, follow, message, publish or
    /// see another member.
    /// </para>
    /// </summary>
    Onboarding = 0,

    /// <summary>
    /// Ordinary participation, available to anyone who has completed onboarding.
    /// <para>
    /// Reading feeds, commenting, reacting, sharing, saving, following, managing your own
    /// account, asking to be verified. The bulk of the platform.
    /// </para>
    /// </summary>
    Member = 1,

    /// <summary>
    /// Requires proven identity, because the cost of getting it wrong falls on other people.
    /// <para>
    /// Posting into a neighbourhood feed, publishing a guide, creating a listing, sending a
    /// message, uploading video, running a poll. The common thread is <b>broadcast or
    /// expense</b>: a capability that reaches strangers, or that costs the platform real money
    /// to serve.
    /// </para>
    /// </summary>
    Trusted = 2,

    /// <summary>
    /// Granted by a non-staff role rather than by progression.
    /// <para>
    /// A service provider managing their own business listing is the intended shape: no amount
    /// of verification confers it, and holding it implies nothing about how far up the ladder
    /// the member has climbed.
    /// </para>
    /// <para>
    /// <b>No capability on the platform is in this category today.</b> That is not an
    /// oversight — the contributor concept is currently carried by
    /// <c>AccessTier.Verified</c>, whose own documentation calls it "the contributor tier". The
    /// category is defined now so that the first module needing a genuinely role-scoped,
    /// non-staff capability has somewhere correct to put it.
    /// </para>
    /// </summary>
    Contributor = 3,

    /// <summary>
    /// Staff authority, or authority no member holds at all.
    /// <para>
    /// Moderation queues, user suspension, role assignment, catalogue management — and
    /// service-audience permissions such as applying a trust decision, which reach a token only
    /// through <c>IServiceTokenIssuer</c> and never through a tier or a role. Both are grouped
    /// here because both answer the same question: <em>is this authority a member could ever
    /// hold?</em> No.
    /// </para>
    /// </summary>
    Administrative = 4
}
