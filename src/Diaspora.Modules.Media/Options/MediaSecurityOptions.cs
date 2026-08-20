namespace Diaspora.Modules.Media.Options;

/// <summary>
/// How signed links are minted and how strictly access is judged.
/// <para>
/// <see cref="SigningKey"/> is the one secret this module has. It signs the URLs that carry
/// access to private assets, including immigration documents, and a deployment that leaves it
/// empty outside Development fails to start rather than falling back to a default key — the
/// same posture the Identity module takes with its JWT signing key, and for the same reason: a
/// well-known default key is worse than no signing at all, because it looks like security.
/// </para>
/// </summary>
public sealed class MediaSecurityOptions
{
    public const string SectionName = "Media:Security";

    /// <summary>
    /// The HMAC key for signed URLs. At least 32 bytes. Required outside Development.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Default lifetime of a signed link, in minutes.</summary>
    public int SignedUrlMinutes { get; set; } = 15;

    /// <summary>
    /// Lifetime of a link to a verification document, in minutes. Shorter, and matched to
    /// Trust's own <c>DocumentLinkFactory.LinkLifetime</c>.
    /// </summary>
    public int SensitiveSignedUrlMinutes { get; set; } = 10;

    /// <summary>
    /// Whether an asset may be served before a scanner has cleared it.
    /// <para>
    /// False in production. True is for a development machine with no scanner sidecar, where
    /// the alternative is that no uploaded image ever renders. The readiness check reports the
    /// setting, so a deployment cannot be in this state without it being visible.
    /// </para>
    /// </summary>
    public bool AllowUnscannedReads { get; set; }

    /// <summary>
    /// How long a member may download before the limiter refuses — requests per minute.
    /// <para>
    /// Downloads are limited separately from the rest of the API because the resource is
    /// bandwidth rather than compute, and a limit tuned for JSON endpoints is either useless
    /// for a gallery of forty photographs or crippling for it.
    /// </para>
    /// </summary>
    public int DownloadsPerMinute { get; set; } = 300;
}
