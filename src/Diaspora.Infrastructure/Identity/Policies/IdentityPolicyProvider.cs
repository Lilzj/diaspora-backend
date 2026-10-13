using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.Policies;
using Diaspora.Infrastructure.Identity.Options;
using Microsoft.Extensions.Options;

namespace Diaspora.Infrastructure.Identity.Policies;

/// <summary>
/// Projects the options classes into the single <see cref="IdentityPolicySet"/> the
/// Application layer consumes.
/// <para>
/// Uses <c>IOptionsMonitor</c> so a configuration reload takes effect without a restart,
/// and rebuilds the set per read rather than caching it — these are cheap objects, and a
/// cached copy would quietly ignore the reload it exists to support.
/// </para>
/// </summary>
public sealed class IdentityPolicyProvider : IIdentityPolicyProvider
{
    private readonly IOptionsMonitor<OtpOptions> _otp;
    private readonly IOptionsMonitor<LockoutOptions> _lockout;
    private readonly IOptionsMonitor<PasswordPolicyOptions> _password;
    private readonly IOptionsMonitor<RefreshTokenOptions> _refreshToken;
    private readonly IOptionsMonitor<JwtOptions> _jwt;
    private readonly IOptionsMonitor<IdentityModuleOptions> _identity;

    public IdentityPolicyProvider(
        IOptionsMonitor<OtpOptions> otp,
        IOptionsMonitor<LockoutOptions> lockout,
        IOptionsMonitor<PasswordPolicyOptions> password,
        IOptionsMonitor<RefreshTokenOptions> refreshToken,
        IOptionsMonitor<JwtOptions> jwt,
        IOptionsMonitor<IdentityModuleOptions> identity)
    {
        _otp = otp;
        _lockout = lockout;
        _password = password;
        _refreshToken = refreshToken;
        _jwt = jwt;
        _identity = identity;
    }

    public IdentityPolicySet Current
    {
        get
        {
            var otp = _otp.CurrentValue;
            var lockout = _lockout.CurrentValue;
            var password = _password.CurrentValue;
            var refreshToken = _refreshToken.CurrentValue;

            return new IdentityPolicySet
            {
                Otp = OtpPolicy.Create(
                    TimeSpan.FromMinutes(otp.LifetimeMinutes),
                    otp.MaxVerificationAttempts,
                    otp.MaxResends,
                    TimeSpan.FromSeconds(otp.ResendCooldownSeconds)),

                Lockout = LockoutPolicy.Create(
                    lockout.MaxFailedAttempts,
                    TimeSpan.FromMinutes(lockout.LockoutMinutes)),

                PasswordHistory = password.EnforceHistory
                    ? PasswordHistoryPolicy.Create(password.HistoryDepth, enforced: true)
                    : PasswordHistoryPolicy.Disabled,

                RefreshTokenLifetime = refreshToken.IdleLifetimeMinutes > 0
                    ? TimeSpan.FromMinutes(refreshToken.IdleLifetimeMinutes)
                    : TimeSpan.FromDays(refreshToken.LifetimeDays),
                AbsoluteSessionLifetime = TimeSpan.FromDays(refreshToken.AbsoluteSessionLifetimeDays),
                IncludeFeaturesInAccessToken = _jwt.CurrentValue.IncludeFeatureClaims,
                MaxDevicesPerUser = _identity.CurrentValue.MaxDevicesPerUser,

                PasswordMaxAge = password.MaximumAgeDays > 0
                    ? TimeSpan.FromDays(password.MaximumAgeDays)
                    : null,

                MaxRecoveryRequestsPerWindow = otp.MaxRequestsPerWindow,
                RecoveryThrottleWindow = TimeSpan.FromMinutes(otp.ThrottleWindowMinutes)
            };
        }
    }
}
