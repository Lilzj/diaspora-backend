namespace Diaspora.Platform.Operations.Security;

/// <summary>Where a secret is in its life.</summary>
public enum SecretStatus
{
    /// <summary>In use for signing and for verifying.</summary>
    Active = 0,

    /// <summary>
    /// Superseded, still accepted for verification.
    /// <para>
    /// The state that makes rotation possible without an outage. A signing key that went straight
    /// from active to retired would invalidate every token issued under it at the instant of
    /// rotation, which signs every member out at once.
    /// </para>
    /// </summary>
    Retiring = 1,

    /// <summary>No longer accepted for anything.</summary>
    Retired = 2,

    /// <summary>Past its expiry and still in use. A finding, not a state anything should be in.</summary>
    Expired = 3
}

/// <summary>
/// What the platform knows about a secret, without knowing the secret.
/// <para>
/// Everything on the operational surface — the dashboard, the health check, the expiry alert — reads
/// this and never a value. A rotation dashboard that had to load secrets to display their expiry dates
/// would be a dashboard that collects every secret on the platform into one response.
/// </para>
/// </summary>
/// <param name="Name">The secret's name. Dotted, namespaced to the module that needs it.</param>
/// <param name="Version">Increments on rotation. The version is what makes two live values addressable.</param>
/// <param name="Status">Where it is.</param>
/// <param name="CreatedAt">When this version was created.</param>
/// <param name="ExpiresAt">When it must be rotated by. Null means no policy.</param>
/// <param name="RotatedAt">When it was last rotated.</param>
/// <param name="Fingerprint">
/// A short hash of the value, for confirming that two deployments hold the same secret without either
/// revealing it. Truncated deliberately: enough to compare, not enough to attack.
/// </param>
public sealed record SecretDescriptor(
    string Name,
    int Version,
    SecretStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RotatedAt,
    string Fingerprint)
{
    /// <summary>Whether this secret is past its expiry.</summary>
    public bool IsExpired(DateTimeOffset now) => ExpiresAt is { } expiry && now >= expiry;

    /// <summary>Whether it expires within <paramref name="window"/>.</summary>
    public bool IsExpiringSoon(DateTimeOffset now, TimeSpan window) =>
        ExpiresAt is { } expiry && expiry - now <= window && now < expiry;
}

/// <summary>
/// A secret's value, held for as short a time as possible.
/// <para>
/// A record rather than a bare string so that a value is always accompanied by the version it came
/// from. A caller that verifies a signature needs to know which key verified it — for the audit trail,
/// and to decide whether the caller should be asked to re-authenticate under the current one.
/// </para>
/// </summary>
public sealed record SecretValue(string Name, int Version, string Value);

/// <summary>
/// Where secrets come from.
/// <para>
/// <b>The platform has never had one, and the absence has a shape.</b> Signing keys, provider
/// credentials and webhook secrets are read from <c>IConfiguration</c>, which means they are in an
/// environment variable or a mounted file, which means rotation is a redeploy and expiry is something
/// nobody tracks. This is the seam a real secret manager registers into; what ships behind it reads
/// the same configuration, so nothing changes on day one except that rotation and expiry become
/// things the platform can express and report on.
/// </para>
/// <para>
/// <b>No method here returns every secret.</b> Enumeration is over descriptors only. A store that
/// could be asked for all its values is one call away from being a full export.
/// </para>
/// </summary>
public interface ISecretStore
{
    /// <summary>The active value, or null when the secret is unknown.</summary>
    Task<SecretValue?> GetAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every value that is still accepted — the active one, plus anything retiring.
    /// <para>
    /// What a token validator needs: it must accept a signature made under the previous key while
    /// that key is retiring, and reject it once the key is retired.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<SecretValue>> GetAcceptedAsync(
        string name, CancellationToken cancellationToken = default);

    /// <summary>Metadata for every known secret. Never values.</summary>
    Task<IReadOnlyList<SecretDescriptor>> DescribeAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Metadata for one secret, or null.</summary>
    Task<SecretDescriptor?> DescribeAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Installs a new version, moving the current one to <see cref="SecretStatus.Retiring"/>.
    /// </summary>
    /// <param name="retirementWindow">
    /// How long the superseded version stays accepted. Must exceed the lifetime of anything signed
    /// under it — for an access token, its expiry — or rotation invalidates live tokens.
    /// </param>
    Task<SecretDescriptor> RotateAsync(
        string name,
        string newValue,
        TimeSpan retirementWindow,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether every declared secret is present, unexpired and well-formed.
    /// <para>
    /// Run at startup and by the health check. A platform that discovers a missing webhook secret when
    /// the first webhook arrives discovers it as a stream of rejected callbacks from a payment provider.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<string>> ValidateAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Declares a secret the module needs, so the platform can validate and report on it without the
/// module being asked.
/// </summary>
/// <param name="Name">The secret's name.</param>
/// <param name="Module">Who needs it.</param>
/// <param name="Description">What it is for.</param>
/// <param name="Required">Whether the module can run without it.</param>
/// <param name="MinimumLength">Shortest acceptable value. A signing key that is too short is a real finding.</param>
/// <param name="RotateEvery">How often it should be rotated. Null means no schedule.</param>
public sealed record SecretRequirement(
    string Name,
    string Module,
    string Description,
    bool Required = true,
    int MinimumLength = 0,
    TimeSpan? RotateEvery = null);

/// <summary>Declares a module's secret requirements.</summary>
public interface ISecretRequirementContributor
{
    string Module { get; }

    IEnumerable<SecretRequirement> Secrets { get; }
}
