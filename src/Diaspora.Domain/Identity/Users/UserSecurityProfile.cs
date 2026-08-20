using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Users;

/// <summary>
/// The security metadata carried by every account: failed sign-in counters, the
/// self-healing lockout window, and password/verification timestamps.
/// <para>
/// Immutable — each mutation returns a replacement, which keeps the aggregate's
/// state transitions explicit and makes the type trivially testable.
/// </para>
/// </summary>
public sealed class UserSecurityProfile : ValueObject
{
    public int FailedLoginAttempts { get; }
    public DateTimeOffset? LastFailedLoginAt { get; }
    public DateTimeOffset? LockoutEndsAt { get; }
    public DateTimeOffset? PasswordChangedAt { get; }
    public DateTimeOffset? PhoneVerifiedAt { get; }
    public DateTimeOffset? EmailVerifiedAt { get; }
    public bool RequiresPasswordChange { get; }

    /// <summary>
    /// Incremented whenever every token in existence must stop working — a password
    /// reset, a hard lock, or a "revoke all sessions". Access tokens carry this value,
    /// so a stale token fails validation without a database round trip.
    /// </summary>
    public int SecurityStamp { get; }

    private UserSecurityProfile(
        int failedLoginAttempts,
        DateTimeOffset? lastFailedLoginAt,
        DateTimeOffset? lockoutEndsAt,
        DateTimeOffset? passwordChangedAt,
        DateTimeOffset? phoneVerifiedAt,
        DateTimeOffset? emailVerifiedAt,
        bool requiresPasswordChange,
        int securityStamp)
    {
        FailedLoginAttempts = failedLoginAttempts;
        LastFailedLoginAt = lastFailedLoginAt;
        LockoutEndsAt = lockoutEndsAt;
        PasswordChangedAt = passwordChangedAt;
        PhoneVerifiedAt = phoneVerifiedAt;
        EmailVerifiedAt = emailVerifiedAt;
        RequiresPasswordChange = requiresPasswordChange;
        SecurityStamp = securityStamp;
    }

    public static UserSecurityProfile Initial(DateTimeOffset now) =>
        new(0, null, null, now, null, null, false, 1);

    public bool IsLockedOut(DateTimeOffset now) => LockoutEndsAt is { } ends && ends > now;

    public TimeSpan LockoutRemaining(DateTimeOffset now) =>
        IsLockedOut(now) ? LockoutEndsAt!.Value - now : TimeSpan.Zero;

    /// <summary>
    /// Records a failed sign-in. Once <paramref name="maxAttempts"/> is reached the
    /// account enters a temporary lockout of <paramref name="lockoutDuration"/>; the
    /// access state is untouched.
    /// </summary>
    public UserSecurityProfile RecordFailedLogin(DateTimeOffset now, int maxAttempts, TimeSpan lockoutDuration)
    {
        var attempts = FailedLoginAttempts + 1;
        var lockoutEnds = attempts >= maxAttempts ? now.Add(lockoutDuration) : LockoutEndsAt;

        return new UserSecurityProfile(
            attempts, now, lockoutEnds, PasswordChangedAt,
            PhoneVerifiedAt, EmailVerifiedAt, RequiresPasswordChange, SecurityStamp);
    }

    public UserSecurityProfile ClearFailedLogins() =>
        new(0, LastFailedLoginAt, null, PasswordChangedAt,
            PhoneVerifiedAt, EmailVerifiedAt, RequiresPasswordChange, SecurityStamp);

    public UserSecurityProfile WithPasswordChanged(DateTimeOffset now) =>
        new(0, LastFailedLoginAt, null, now,
            PhoneVerifiedAt, EmailVerifiedAt, false, SecurityStamp + 1);

    public UserSecurityProfile WithPhoneVerified(DateTimeOffset now) =>
        new(0, LastFailedLoginAt, null, PasswordChangedAt,
            now, EmailVerifiedAt, RequiresPasswordChange, SecurityStamp);

    public UserSecurityProfile WithEmailVerified(DateTimeOffset now) =>
        new(FailedLoginAttempts, LastFailedLoginAt, LockoutEndsAt, PasswordChangedAt,
            PhoneVerifiedAt, now, RequiresPasswordChange, SecurityStamp);

    public UserSecurityProfile WithPasswordChangeRequired() =>
        new(FailedLoginAttempts, LastFailedLoginAt, LockoutEndsAt, PasswordChangedAt,
            PhoneVerifiedAt, EmailVerifiedAt, true, SecurityStamp);

    public UserSecurityProfile WithLockout(DateTimeOffset until) =>
        new(FailedLoginAttempts, LastFailedLoginAt, until, PasswordChangedAt,
            PhoneVerifiedAt, EmailVerifiedAt, RequiresPasswordChange, SecurityStamp);

    /// <summary>
    /// Invalidates every access token issued so far.
    /// </summary>
    public UserSecurityProfile WithRotatedSecurityStamp() =>
        new(FailedLoginAttempts, LastFailedLoginAt, LockoutEndsAt, PasswordChangedAt,
            PhoneVerifiedAt, EmailVerifiedAt, RequiresPasswordChange, SecurityStamp + 1);

    /// <summary>
    /// Whether the password is older than <paramref name="maxAge"/>. Exposed as a hook
    /// for a future password-expiry policy; nothing enforces it today.
    /// </summary>
    public bool IsPasswordExpired(DateTimeOffset now, TimeSpan? maxAge) =>
        maxAge is { } age && PasswordChangedAt is { } changed && changed.Add(age) <= now;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return FailedLoginAttempts;
        yield return LastFailedLoginAt;
        yield return LockoutEndsAt;
        yield return PasswordChangedAt;
        yield return PhoneVerifiedAt;
        yield return EmailVerifiedAt;
        yield return RequiresPasswordChange;
        yield return SecurityStamp;
    }
}
