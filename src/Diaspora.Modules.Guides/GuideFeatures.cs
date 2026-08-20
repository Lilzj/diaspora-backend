namespace Diaspora.Modules.Guides;

/// <summary>
/// The feature gates the Guides module owns.
/// <para>
/// Dot-separated names with the module prefix. Every gate's floor is at least
/// <c>AccessTier.Limited</c> — a gate below Limited is granted to phone-unverified accounts,
/// which is the B6 lesson made policy. Reading published guides needs no gate at all: public
/// knowledge is public, and the anonymous read path simply carries no authorization
/// requirement.
/// </para>
/// </summary>
public static class GuideFeatures
{
    /// <summary>
    /// Browsing published guides. **Claimed from the provisional catalogue in PSR-1** —
    /// B2 declared it before this module existed and the host carried it as
    /// "Platform (provisional)" for twelve phases. Same name, same Limited floor; only the
    /// owner changed, which is backend attribution and reaches no client.
    /// <para>
    /// Reading a published guide still needs no gate — see the class summary. This one
    /// exists because the mobile client renders a lock card against the name.
    /// </para>
    /// </summary>
    public const string Browse = "guides.browse";

    /// <summary>Draft and edit guides.</summary>
    public const string Author = "guides.author";

    /// <summary>Publish guides as long-lived knowledge. Verified members only.</summary>
    public const string Publish = "guides.publish";

    /// <summary>Vote, bookmark, share and report guides.</summary>
    public const string Engage = "guides.engage";

    /// <summary>Propose improvements to other members' guides.</summary>
    public const string Suggest = "guides.suggest";

    /// <summary>Submit a guide for Trust verification.</summary>
    public const string Verify = "guides.verify";

    /// <summary>Work the guide moderation queue. Staff only.</summary>
    public const string Moderate = "guides.moderate";
}
