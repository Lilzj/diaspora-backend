using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.Devices;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Events;
using Diaspora.Domain.Identity.ValueObjects;
using Diaspora.SharedKernel;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Domain.Identity.Sessions;

/// <summary>
/// A signed-in session on one device, and the consistency boundary for refresh-token
/// rotation.
/// <para>
/// Sessions are their own aggregate rather than a collection inside <c>User</c>: a
/// long-lived account accumulates many of them, and loading every historical session
/// to authenticate one request would be indefensible. The trade-off is that
/// "revoke everything" is a repository-level operation, which the application layer
/// performs explicitly.
/// </para>
/// </summary>
public sealed class UserSession : AggregateRoot<SessionId>, IAuditableEntity
{
    public const int MaxDeviceNameLength = 120;

    private readonly List<RefreshToken> _refreshTokens = [];

    public UserId UserId { get; private set; } = default!;
    public DeviceId? DeviceId { get; private set; }
    public string DeviceName { get; private set; } = default!;
    public DevicePlatform Platform { get; private set; }
    public string? AppVersion { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    public SessionStatus Status { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }

    /// <summary>
    /// The hard ceiling on this session's life. Rotation extends the refresh token
    /// but never this, so a stolen device cannot keep a session alive indefinitely.
    /// </summary>
    public DateTimeOffset AbsoluteExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }
    public SessionRevocationReason? RevocationReason { get; private set; }

    public IReadOnlyList<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    private UserSession() { }

    private UserSession(
        SessionId id,
        UserId userId,
        DeviceId? deviceId,
        string deviceName,
        DevicePlatform platform,
        string? appVersion,
        string? ipAddress,
        string? userAgent,
        DateTimeOffset now,
        DateTimeOffset absoluteExpiresAt) : base(id)
    {
        UserId = userId;
        DeviceId = deviceId;
        DeviceName = deviceName;
        Platform = platform;
        AppVersion = appVersion;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        Status = SessionStatus.Active;
        LastSeenAt = now;
        AbsoluteExpiresAt = absoluteExpiresAt;
    }

    /// <summary>
    /// Opens a session and mints its first refresh token in one step — a session
    /// without a token would be meaningless, so the two are never separable.
    /// </summary>
    public static UserSession Start(
        UserId userId,
        DeviceId? deviceId,
        string? deviceName,
        DevicePlatform platform,
        string? appVersion,
        string? ipAddress,
        string? userAgent,
        SecretHash refreshTokenHash,
        DateTimeOffset now,
        TimeSpan refreshTokenLifetime,
        TimeSpan absoluteSessionLifetime)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(refreshTokenHash);

        var name = string.IsNullOrWhiteSpace(deviceName) ? "Unknown device" : deviceName.Trim();
        Guard.Against.MaxLength(name, MaxDeviceNameLength, nameof(deviceName));

        var session = new UserSession(
            SessionId.New(),
            userId,
            deviceId,
            name,
            platform,
            Truncate(appVersion, 40),
            Truncate(ipAddress, 64),
            Truncate(userAgent, 400),
            now,
            now.Add(absoluteSessionLifetime));

        var token = RefreshToken.Issue(
            session.Id,
            refreshTokenHash,
            now,
            Min(now.Add(refreshTokenLifetime), session.AbsoluteExpiresAt));

        session._refreshTokens.Add(token);

        session.RaiseDomainEvent(new UserSessionCreatedDomainEvent(
            userId, session.Id, deviceId, name, platform, now));
        session.RaiseDomainEvent(new RefreshTokenIssuedDomainEvent(
            userId, session.Id, token.Id, token.ExpiresAt));

        return session;
    }

    /// <summary>The token a client should currently be holding, if any.</summary>
    public RefreshToken? CurrentRefreshToken(DateTimeOffset now) =>
        _refreshTokens.FirstOrDefault(t => t.IsRedeemableAt(now));

    public bool IsActiveAt(DateTimeOffset now) =>
        Status == SessionStatus.Active && AbsoluteExpiresAt > now;

    /// <summary>
    /// Exchanges a presented refresh token for a fresh one.
    /// <para>
    /// If the presented token was already rotated or revoked, the whole session is
    /// torn down before returning: a legitimate client never replays a token, so the
    /// safe reading is that the chain has leaked. Killing the session logs out both
    /// the attacker and the genuine member, which is the correct trade.
    /// </para>
    /// </summary>
    public Result<RefreshToken> RotateRefreshToken(
        SecretHash presentedHash,
        SecretHash replacementHash,
        DateTimeOffset now,
        TimeSpan refreshTokenLifetime)
    {
        ArgumentNullException.ThrowIfNull(presentedHash);
        ArgumentNullException.ThrowIfNull(replacementHash);

        var presented = _refreshTokens.FirstOrDefault(t => t.TokenHash.Matches(presentedHash));
        if (presented is null)
        {
            return Result.Failure<RefreshToken>(SessionErrors.RefreshTokenNotFound);
        }

        if (presented.WasConsumed)
        {
            RevokeInternal(SessionRevocationReason.TokenReuseDetected, now);
            RaiseDomainEvent(new RefreshTokenReuseDetectedDomainEvent(UserId, Id, presented.Id, now));
            return Result.Failure<RefreshToken>(SessionErrors.RefreshTokenReuseDetected);
        }

        if (Status != SessionStatus.Active)
        {
            return Result.Failure<RefreshToken>(SessionErrors.NotActive);
        }

        if (AbsoluteExpiresAt <= now)
        {
            ExpireInternal(now);
            return Result.Failure<RefreshToken>(SessionErrors.Expired);
        }

        if (!presented.IsRedeemableAt(now))
        {
            return Result.Failure<RefreshToken>(SessionErrors.RefreshTokenExpired);
        }

        var replacement = RefreshToken.Issue(
            Id,
            replacementHash,
            now,
            Min(now.Add(refreshTokenLifetime), AbsoluteExpiresAt));

        presented.MarkRotated(now, replacement.Id);
        _refreshTokens.Add(replacement);
        LastSeenAt = now;

        RaiseDomainEvent(new RefreshTokenRotatedDomainEvent(UserId, Id, presented.Id, replacement.Id, now));

        return Result.Success(replacement);
    }

    public Result Revoke(SessionRevocationReason reason, DateTimeOffset now)
    {
        if (Status == SessionStatus.Revoked)
        {
            return Result.Failure(SessionErrors.AlreadyRevoked);
        }

        RevokeInternal(reason, now);

        if (reason == SessionRevocationReason.SignedOut)
        {
            RaiseDomainEvent(new UserLoggedOutDomainEvent(UserId, Id, now));
        }

        return Result.Success();
    }

    /// <summary>
    /// Refreshes the "last seen" heartbeat shown in the member's session list.
    /// </summary>
    public void Touch(DateTimeOffset now)
    {
        if (Status != SessionStatus.Active) return;

        if (AbsoluteExpiresAt <= now)
        {
            ExpireInternal(now);
            return;
        }

        LastSeenAt = now;
    }

    private void RevokeInternal(SessionRevocationReason reason, DateTimeOffset now)
    {
        Status = SessionStatus.Revoked;
        RevokedAt = now;
        RevocationReason = reason;

        foreach (var token in _refreshTokens)
        {
            token.MarkRevoked(now);
        }

        RaiseDomainEvent(new UserSessionRevokedDomainEvent(UserId, Id, reason, now));
    }

    private void ExpireInternal(DateTimeOffset now)
    {
        Status = SessionStatus.Expired;
        RevokedAt = now;
        RevocationReason = SessionRevocationReason.Expired;

        foreach (var token in _refreshTokens)
        {
            token.MarkRevoked(now);
        }

        RaiseDomainEvent(new UserSessionRevokedDomainEvent(
            UserId, Id, SessionRevocationReason.Expired, now));
    }

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) =>
        left <= right ? left : right;

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }
}
