using Diaspora.Domain.Identity.Policies;

namespace Diaspora.Application.Identity.Models;

/// <summary>
/// Every tunable the identity use cases obey, resolved once from configuration.
/// <para>
/// Handlers take this instead of <c>IOptions&lt;T&gt;</c> so the Application layer never
/// touches the configuration system directly, and so a test can pin an entire policy set
/// in one object rather than stubbing six option types.
/// </para>
/// </summary>
public sealed record IdentityPolicySet
{
    public required OtpPolicy Otp { get; init; }

    public required LockoutPolicy Lockout { get; init; }

    public required PasswordHistoryPolicy PasswordHistory { get; init; }

    public required TimeSpan RefreshTokenLifetime { get; init; }

    /// <summary>
    /// Hard ceiling on a session's life, regardless of how often its refresh token rotates.
    /// </summary>
    public required TimeSpan AbsoluteSessionLifetime { get; init; }

    /// <summary>
    /// Whether to embed the feature-gate snapshot in the access token. Off by default:
    /// it inflates every request's headers, and the gate list is available from
    /// <c>GET /access/features</c>.
    /// </summary>
    public required bool IncludeFeaturesInAccessToken { get; init; }

    public required int MaxDevicesPerUser { get; init; }

    /// <summary>Null disables password expiry. Nothing enforces it in B2.</summary>
    public TimeSpan? PasswordMaxAge { get; init; }

    /// <summary>How many recovery codes a member may request inside <see cref="RecoveryThrottleWindow"/>.</summary>
    public required int MaxRecoveryRequestsPerWindow { get; init; }

    public required TimeSpan RecoveryThrottleWindow { get; init; }
}
