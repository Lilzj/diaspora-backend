namespace Diaspora.Modules.Housing;

/// <summary>
/// The feature gates the Housing module owns.
/// <para>
/// Dot-separated names with the module prefix. Every gate's floor is at least
/// <c>AccessTier.Limited</c> — a gate below Limited is granted to phone-unverified accounts, which
/// is the B6 lesson made policy. Reading published listings and neighbourhood profiles needs no
/// gate at all: somebody deciding whether to move to a country should be able to see what living
/// there costs before they join anything.
/// </para>
/// <para>
/// <b>None of these names has been declared provisionally by the host.</b> Unlike B8's
/// <c>guides.publish</c>, B9's <c>jobs.apply</c> and B10's <c>marketplace.browse</c>, the
/// provisional catalogue never held a housing entry — so B11 is the first vertical since B7 that
/// deletes nothing on the way in. That is worth stating because the trap those phases each
/// recorded is real: the feature catalogue refuses two modules declaring one gate, and the startup
/// failure takes down every request that resolves it, phone verification included. There is
/// nothing to collide with here, and the check was made rather than assumed.
/// </para>
/// <para>
/// <b>The band is "looking is Limited, committing is Verified".</b> Sharper here than in commerce:
/// booking a viewing unlocks a property's address, and a member who could do that without proving
/// who they are could map the empty homes in a city.
/// </para>
/// </summary>
public static class HousingFeatures
{
    /// <summary>Browse and search housing. The entry point to the whole module.</summary>
    public const string Browse = "housing.browse";

    /// <summary>
    /// List a property. Verified only, because a listing collects deposits from strangers who have
    /// not arrived in the country yet.
    /// </summary>
    public const string List = "housing.listings.create";

    /// <summary>
    /// Arrange to see a property. Verified only — this is the gate that guards addresses.
    /// </summary>
    public const string Viewing = "housing.viewing";

    /// <summary>Reserve a home. Verified only: a reservation takes it off the market.</summary>
    public const string Reserve = "housing.reserve";

    /// <summary>Contribute to a neighbourhood profile. Moderated regardless of gate.</summary>
    public const string Contribute = "housing.neighborhood.contribute";

    /// <summary>Work the housing moderation queue. Staff only.</summary>
    public const string Moderate = "housing.moderate";
}
