namespace Diaspora.Modules.Providers;

/// <summary>
/// The permissions the Providers module owns.
/// <para>
/// The band expresses one product decision repeatedly: <b>browsing is how a newcomer finds the
/// community, offering is a public commitment, and administering the catalogue is staff.</b>
/// Reading opens at <c>Onboarding</c> because a member who cannot see a single restaurant has
/// no reason to finish verifying. Registering a provider opens at <c>Verified</c>, because an
/// unverified account publishing a money-transfer business into a diaspora community is the
/// single worst thing this module can be used for. Reviewing sits at <c>Limited</c>, because a
/// member's first honest review is often written before their verification clears.
/// </para>
/// </summary>
public static class ProviderPermissions
{
    /// <summary>Read providers, services, categories, availability and reviews.</summary>
    public const string Read = "providers:read";

    /// <summary>Register a provider. The highest-consequence write in the module.</summary>
    public const string Register = "providers:register";

    /// <summary>Edit a provider the caller owns or manages.</summary>
    public const string Manage = "providers:manage";

    /// <summary>Publish, pause, close or reopen a provider the caller owns.</summary>
    public const string ManageStatus = "providers:status";

    /// <summary>Add, change and remove owners, managers, moderators and editors.</summary>
    public const string ManageMembers = "providers:members";

    /// <summary>Add, change and remove the services a provider offers.</summary>
    public const string ManageServices = "providers:services";

    /// <summary>Set operating hours, availability exceptions and the availability mode.</summary>
    public const string ManageAvailability = "providers:availability";

    /// <summary>Attach and detach gallery, logo, cover and document media.</summary>
    public const string ManageMedia = "providers:media";

    /// <summary>Submit a provider for identity, business, document or certification verification.</summary>
    public const string SubmitVerification = "providers:verification:submit";

    /// <summary>Write, edit and withdraw the caller's own review of a provider.</summary>
    public const string Review = "providers:review";

    /// <summary>Recommend or endorse a provider.</summary>
    public const string Recommend = "providers:recommend";

    /// <summary>Save a provider. Delegated to Community's bookmark store.</summary>
    public const string Bookmark = "providers:bookmark";

    /// <summary>Read the owner-facing statistics of a provider the caller owns or manages.</summary>
    public const string ReadStatistics = "providers:statistics:read";

    /// <summary>Manage the service category catalogue. Staff only.</summary>
    public const string AdministerCatalog = "providers:catalog:administer";

    /// <summary>Feature, unfeature, suspend and restore any provider. Staff only.</summary>
    public const string Administer = "providers:administer";
}
