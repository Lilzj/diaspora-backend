namespace Diaspora.Platform.Http;

/// <summary>
/// Stable machine-readable codes for failures raised by the platform itself rather than by
/// a module's domain <c>Error</c>.
/// <para>
/// Every error response carries a <c>code</c> extension; this fills it when the failure came
/// from the pipeline or the host. A module's own failures carry the module's own error code
/// (<c>Bookmarks.NotFound</c>) instead. Kept in the kernel so every module's endpoints and
/// the host produce one vocabulary. These are contract — they change only with a version.
/// </para>
/// </summary>
public static class PlatformErrorCodes
{
    public const string Validation = "Platform.Validation.Failed";
    public const string Unauthorized = "Platform.Auth.Unauthorized";
    public const string Forbidden = "Platform.Auth.Forbidden";

    /// <summary>
    /// The caller may do this but has not re-proved their identity recently enough.
    /// <para>
    /// Distinct from <see cref="Forbidden"/> on purpose, and the distinction is the whole reason
    /// this code exists: one is a dead end and the other is an instruction to ask the member for a
    /// code and retry. The response carries a <c>stepUpScope</c> extension naming what to ask for.
    /// </para>
    /// </summary>
    public const string StepUpRequired = "Identity.StepUp.Required";
    public const string NotFound = "Platform.Resource.NotFound";
    public const string Conflict = "Platform.Resource.Conflict";
    public const string ConcurrencyConflict = "Platform.Resource.ConcurrencyConflict";
    public const string BusinessRule = "Platform.Domain.RuleViolation";
    public const string RateLimited = "Platform.RateLimit.Exceeded";
    public const string ServiceUnavailable = "Platform.Dependency.Unavailable";
    public const string Unexpected = "Platform.Unexpected";
    public const string ClientClosedRequest = "Platform.Request.Cancelled";
}
