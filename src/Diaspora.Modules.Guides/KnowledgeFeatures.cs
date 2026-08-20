namespace Diaspora.Modules.Guides;

/// <summary>
/// The feature gates the Knowledge Directory owns.
/// <para>
/// <b>Two, and both staff-only.</b> Gates exist so a client can grey out a control with a reason
/// instead of offering it and failing — and the only controls in this phase that a client might
/// offer and be refused are the curation ones. Reading a country's knowledge needs no gate at all,
/// because it needs no account.
/// </para>
/// <para>
/// Both sit at <c>AccessTier.Limited</c> with the staff flag set, which is how the platform
/// expresses "not a progression": a gate below Limited is granted to phone-unverified accounts, and
/// a gate at Verified would tell a member that verifying their identity unlocks curation. It does
/// not, and it should not.
/// </para>
/// </summary>
public static class KnowledgeFeatures
{
    /// <summary>Arrange country directories, feature guides, manage navigation. Staff only.</summary>
    public const string Curate = "guides.knowledge.curate";

    /// <summary>Write and publish curated onboarding routes. Staff only.</summary>
    public const string ManagePaths = "guides.knowledge.paths";
}
