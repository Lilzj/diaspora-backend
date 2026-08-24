namespace Diaspora.Modules.Housing;

/// <summary>
/// The permissions the Housing module owns.
/// <para>
/// Colon-separated keys with the module prefix, per the platform convention (policies use dots,
/// see <see cref="HousingPolicies"/>). Tier floors live with the definitions in
/// <c>Authorization/HousingPermissionContributor</c>, because the module is the only thing that
/// knows what each capability is worth.
/// </para>
/// <para>
/// <b>The split that matters: looking is Limited, committing is Verified.</b> Browsing properties,
/// reading a neighbourhood profile and reporting a scam are protective or private acts that cost
/// nobody anything. Listing a property, booking a viewing and reserving a home each put a real
/// identity, a stranger's afternoon or somebody's deposit at stake — which is exactly the
/// progression B2.0.1 gates on proven identity.
/// </para>
/// <para>
/// <b>Booking a viewing is Verified, and that is the sharpest line in the module.</b> It is the act
/// that unlocks a property's address. A member who could arrange viewings without proving who they
/// are could harvest the addresses of every empty home in a city, and that is not a
/// hypothetical — it is the reconnaissance step of a real crime.
/// </para>
/// <para>
/// Staff-only capabilities are role-scoped, not tier-scoped, per the access-tier semantics: a tier
/// measures progression, a role grants privilege.
/// </para>
/// </summary>
public static class HousingPermissions
{
    // ── Landlord ───────────────────────────────────────────────

    public const string LandlordCreate = "housing:landlord:create";
    public const string LandlordManage = "housing:landlord:manage";
    public const string LandlordManageAny = "housing:landlord:manage:any";
    public const string LandlordSubmitVerification = "housing:landlord:verify:submit";

    // ── Listing authoring and lifecycle ─────────────────────────────────────────────────

    public const string ListingCreate = "housing:listings:create";
    public const string ListingEditOwn = "housing:listing:edit:own";
    public const string ListingEditAny = "housing:listing:edit:any";
    public const string ListingDeleteOwn = "housing:listing:delete:own";
    public const string ListingPublish = "housing:listing:publish";
    public const string ListingArchive = "housing:listing:archive";
    public const string ListingRestore = "housing:listing:restore";
    public const string ListingMarkOccupied = "housing:listing:mark-occupied";
    public const string ListingFeature = "housing:listing:feature";
    public const string ListingSubmitVerification = "housing:listing:verify:submit";

    // ── Relocation workflow ─────────────────────────────────────────────────

    public const string ViewingSchedule = "housing:viewing:schedule";
    public const string ViewingManage = "housing:viewing:manage";
    public const string ReservationCreate = "housing:reservation:create";
    public const string ReservationManage = "housing:reservation:manage";
    public const string ReviewWrite = "housing:review:write";
    public const string ReviewRespond = "housing:review:respond";

    // ── Neighbourhoods ─────────────────────────────────────────────────

    public const string NeighborhoodManage = "housing:neighborhood:manage";
    public const string NeighborhoodApprove = "housing:neighborhood:approve";
    public const string InsightSubmit = "housing:insight:submit";
    public const string InsightEndorse = "housing:insight:endorse";
    public const string InsightModerate = "housing:insight:moderate";

    // ── Shared ─────────────────────────────────────────────────

    public const string Report = "housing:report";
    public const string ViewAnalytics = "housing:analytics:view";

    // ── Staff ─────────────────────────────────────────────────

    public const string Moderate = "housing:moderate";
    public const string ManageCatalogue = "housing:catalogue:manage";
}
