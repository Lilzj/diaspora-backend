namespace Diaspora.Modules.Guides;

/// <summary>
/// The permissions the Country Intelligence Platform owns.
/// <para>
/// <b>Prefixed <c>guides:intelligence:</c> rather than <c>intelligence:</c>, and the prefix is a
/// statement about ownership.</b> The Guides module owns this capability band; B8.2 is a phase of B8
/// rather than a module beside it. A permission catalogue where <c>intelligence:publish</c> appeared
/// with no module to attribute it to would be the first step towards the second country-knowledge
/// owner this whole phase exists to prevent.
/// </para>
/// <para>
/// <b>Publishing and verifying are separate grants, and that is the one separation of duty in the
/// phase.</b> Anybody on the editorial desk can write a notice. Confirming that an authority really
/// said it, and making it visible to everybody in a country, are distinct decisions — and a workflow
/// where the person who transcribes a circular also approves it is a verification step that verifies
/// nothing.
/// </para>
/// <para>
/// <b>Every one of these is role-scoped and none is tier-scoped.</b> B2.0.1's sharpest application:
/// a tier measures progression and a role grants privilege, and no amount of progression should let a
/// member publish something under a government badge. This is editorial authority over what the
/// platform presents as official information, and it is granted, never earned.
/// </para>
/// <para>
/// <b>There is deliberately no read permission.</b> Reading a country's official information requires
/// nothing at all — a platform that demanded an account before it would show somebody an emergency
/// number has misunderstood what the number is for.
/// </para>
/// </summary>
public static class IntelligencePermissions
{
    /// <summary>Draft, edit, submit, archive and restore official notices.</summary>
    public const string ManageNotices = "guides:intelligence:notices:manage";

    /// <summary>Confirm that an authority really issued a submitted notice, or refuse it.</summary>
    public const string VerifyNotices = "guides:intelligence:notices:verify";

    /// <summary>Make a verified notice visible, and supersede a live one.</summary>
    public const string PublishNotices = "guides:intelligence:notices:publish";

    /// <summary>Add, rename, archive and restore notice categories.</summary>
    public const string ManageCatalogue = "guides:intelligence:catalogue:manage";

    /// <summary>Record issuing authorities and raise verification claims about them.</summary>
    public const string ManageAuthorities = "guides:intelligence:authorities:manage";

    /// <summary>Maintain the embassy directory, emergency information and the holiday calendar.</summary>
    public const string ManageDirectory = "guides:intelligence:directory:manage";

    /// <summary>Write and publish visa and immigration advisories.</summary>
    public const string ManageVisas = "guides:intelligence:visas:manage";

    /// <summary>Read country intelligence usage figures.</summary>
    public const string ReadAnalytics = "guides:intelligence:analytics:read";
}
