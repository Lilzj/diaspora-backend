namespace Diaspora.Modules.Providers;

/// <summary>
/// The policy names the module's endpoints reference.
/// <para>
/// One policy per permission, named with dots where the permission uses colons — the convention
/// B2.6 established and B3, B4, B5 and B6 all follow. The kernel validates at startup that every
/// permission named by a policy exists, so a typo here fails the boot rather than silently
/// denying a route forever.
/// </para>
/// </summary>
public static class ProviderPolicies
{
    public const string Read = "providers.read";

    public const string Register = "providers.register";

    public const string Manage = "providers.manage";

    public const string ManageStatus = "providers.status";

    public const string ManageMembers = "providers.members";

    public const string ManageServices = "providers.services";

    public const string ManageAvailability = "providers.availability";

    public const string ManageMedia = "providers.media";

    public const string SubmitVerification = "providers.verification.submit";

    public const string Review = "providers.review";

    public const string Recommend = "providers.recommend";

    public const string Bookmark = "providers.bookmark";

    public const string ReadStatistics = "providers.statistics.read";

    public const string AdministerCatalog = "providers.catalog.administer";

    public const string Administer = "providers.administer";
}
