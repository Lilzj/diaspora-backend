namespace Diaspora.Modules.Providers.Domain.Policies;

/// <summary>
/// The ceilings the aggregates enforce.
/// <para>
/// Constants in the domain rather than options in configuration, and the reason is the one B6
/// records for its own limits: a bound that can be raised per deployment is a bound the domain
/// cannot rely on, and every invariant that depends on it becomes conditional. These are the
/// numbers the aggregate is correct at. A deployment that wants a different number is asking
/// for a different domain rule, and that is a code change with a test.
/// </para>
/// <para>
/// Every one is generous — none of them is a business rule dressed as a limit. They exist to
/// bound the size of a single aggregate so that loading one provider is a predictable amount of
/// work, and to stop an automated client from writing forty thousand gallery rows before anyone
/// notices.
/// </para>
/// </summary>
public static class ProviderLimits
{
    /// <summary>
    /// Providers one member may own. High enough for a franchise operator, low enough that a
    /// scripted sign-up cannot fill the directory.
    /// </summary>
    public const int MaxProvidersPerOwner = 25;

    /// <summary>People attached to one provider, across every role.</summary>
    public const int MaxMembers = 100;

    /// <summary>
    /// Services on one provider. A restaurant menu is the stress case, and a hundred lines is
    /// past every real one in the client's mock data by two orders of magnitude.
    /// </summary>
    public const int MaxServices = 200;

    /// <summary>Time-bounded offerings on one service.</summary>
    public const int MaxOfferingsPerService = 20;

    /// <summary>Service areas on one provider.</summary>
    public const int MaxServiceAreas = 50;

    /// <summary>Physical locations one provider operates from.</summary>
    public const int MaxLocations = 25;

    /// <summary>Languages listed on one provider.</summary>
    public const int MaxLanguages = 20;

    /// <summary>Contact methods on one provider.</summary>
    public const int MaxContactMethods = 15;

    /// <summary>Gallery items on one provider.</summary>
    public const int MaxGalleryItems = 60;

    /// <summary>Certifications on one provider.</summary>
    public const int MaxCertifications = 30;

    /// <summary>Free-text tags on one provider. The client renders these as chips.</summary>
    public const int MaxTags = 15;

    /// <summary>Characters in one tag.</summary>
    public const int MaxTagLength = 40;

    /// <summary>
    /// Opening periods in one day. Three covers a split shift with a prayer break, which is the
    /// most complicated real schedule in the frontend's own data.
    /// </summary>
    public const int MaxWindowsPerDay = 4;

    /// <summary>Dated availability exceptions held at once. A year of holidays and then some.</summary>
    public const int MaxAvailabilityExceptions = 100;

    /// <summary>
    /// How deep the service category tree may go. Three is Food → Restaurants → Ghanaian, and
    /// nobody browsing a directory on a phone wants a fourth tap.
    /// </summary>
    public const int MaxCategoryDepth = 3;

    /// <summary>Aliases one category may answer to.</summary>
    public const int MaxCategoryAliases = 25;

    /// <summary>Search keywords on one category.</summary>
    public const int MaxCategoryKeywords = 50;

    /// <summary>Characters in a review body. The client's own <c>maxLength={500}</c>.</summary>
    public const int MaxReviewBodyLength = 500;

    /// <summary>Characters in a recommendation note.</summary>
    public const int MaxRecommendationNoteLength = 1_000;

    /// <summary>
    /// How long after writing a review its author may still edit it.
    /// <para>
    /// A window rather than "forever", because a review that can be rewritten at any time can be
    /// used as leverage — a five-star review edited down to one star a year later, after the
    /// provider declined a refund, is a pattern every review platform eventually meets. Thirty
    /// days is long enough to fix a typo or reconsider, short enough not to be a weapon.
    /// Withdrawing stays available forever.
    /// </para>
    /// </summary>
    public static readonly TimeSpan ReviewEditWindow = TimeSpan.FromDays(30);

    /// <summary>The largest service radius a provider may claim, in kilometres.</summary>
    public const double MaxServiceRadiusKm = 500d;

    /// <summary>Providers returned in one page of discovery.</summary>
    public const int MaxPageSize = 50;

    public const int DefaultPageSize = 20;

    /// <summary>Providers returned to Location's nearby aggregation in one call.</summary>
    public const int MaxNearbyResults = 25;

    /// <summary>Similar providers returned beside a profile.</summary>
    public const int MaxSimilarResults = 10;
}
