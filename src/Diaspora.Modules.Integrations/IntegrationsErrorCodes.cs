namespace Diaspora.Modules.Integrations;

/// <summary>
/// The wire codes this module returns.
/// <para>
/// Dotted, module-prefixed and stable. Every one maps onto exactly one member of the frontend's
/// <c>IntegrationErrorKind</c> union (<c>types/integrations.ts</c>), which is the vocabulary a
/// screen switches on — <c>lib/integrations/errors.ts</c> does the mapping, and the mapping is what
/// stops a screen ever seeing a provider's own error code. The comment above each block names the
/// kind it becomes.
/// </para>
/// </summary>
public static class IntegrationsErrorCodes
{
    // Every constant below is returned by a real path. Four more were written during this phase —
    // a separate grant-expired, a scope refusal, an outbound-host refusal and a not-yours — and all
    // four were deleted rather than kept: `ReauthorizationRequired` already covers the first, scopes
    // are narrowed rather than refused, a blocked destination never becomes a platform error code,
    // and there is no path where a caller reaches another owner's connection because the repository
    // has no method that would return one. PSR-1.2's rule: a stub is worse than an absence, and a
    // declared code nothing returns is a stub a client would one day switch on.

    private const string Prefix = "integrations.";

    // ── ConfigurationMissing ────────────────────────────────────────────────────────────────
    //
    // The deployment has no adapter, or has one with no credential. Distinct from every failure
    // below because nothing was called and nothing is wrong: a client renders "this deployment
    // does not offer that" rather than "something went wrong".

    public const string ProviderNotConfigured = Prefix + "provider.not-configured";
    public const string ProviderUnknown = Prefix + "provider.unknown";
    public const string CapabilityUnsupported = Prefix + "capability.unsupported";

    // ── AuthenticationRequired / ExpiredCredential ──────────────────────────────────────────

    public const string NotAuthenticated = Prefix + "not-authenticated";
    public const string ConnectionNotFound = Prefix + "connection.not-found";
    public const string ConnectionNotUsable = Prefix + "connection.not-usable";
    public const string ReauthorizationRequired = Prefix + "connection.reauthorization-required";

    // ── InvalidRequest ──────────────────────────────────────────────────────────────────────

    public const string AuthorizationStateUnknown = Prefix + "oauth.state-unknown";
    public const string AuthorizationStateExpired = Prefix + "oauth.state-expired";
    public const string RedirectUriNotAllowed = Prefix + "oauth.redirect-not-allowed";
    public const string IntentUnsupported = Prefix + "oauth.intent-unsupported";
    public const string TimeZoneRequired = Prefix + "calendar.timezone-required";
    public const string TimeZoneUnknown = Prefix + "calendar.timezone-unknown";
    public const string CalendarNotFound = Prefix + "calendar.not-found";
    public const string CalendarEventNotFound = Prefix + "calendar.event.not-found";
    public const string SyncDirectionUnsupported = Prefix + "calendar.sync.direction-unsupported";
    public const string WebhookPayloadTooLarge = Prefix + "webhook.payload-too-large";
    public const string WebhookSignatureInvalid = Prefix + "webhook.signature-invalid";
    public const string WebhookProviderUnknown = Prefix + "webhook.provider-unknown";

    // ── PermissionDenied ────────────────────────────────────────────────────────────────────

    public const string CalendarReadOnly = Prefix + "calendar.read-only";
    public const string ReauthenticationRequired = Prefix + "identity.reauthentication-required";

    // ── Conflict ────────────────────────────────────────────────────────────────────────────
    //
    // The 409 on `oauth.code-replayed` and the 409 on `identity.already-linked` are both security
    // controls rather than tidiness. The register is explicit about the second: without it,
    // "linking is an account merge anybody can trigger".

    public const string AuthorizationCodeReplayed = Prefix + "oauth.code-replayed";
    public const string ExternalAccountAlreadyLinked = Prefix + "identity.already-linked";
    public const string LastAuthenticationMethod = Prefix + "identity.last-method";

    // ── RateLimited ─────────────────────────────────────────────────────────────────────────

    public const string ProviderRateLimited = Prefix + "provider.rate-limited";

    // ── ProviderUnavailable ─────────────────────────────────────────────────────────────────

    public const string ProviderUnavailable = Prefix + "provider.unavailable";
    public const string ProviderRejected = Prefix + "provider.rejected";
}
