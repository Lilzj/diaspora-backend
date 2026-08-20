using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Errors;
using Diaspora.Modules.Notifications.Domain.Events;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.Modules.Notifications.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Domain.Devices;

/// <summary>
/// One place a member can be reached, for one channel.
/// <para>
/// <b>This is not Identity's device, and the distinction was the hardest call in the phase.</b>
/// Identity already stores a <c>UserDevice</c> with a push token it explicitly never uses — a
/// security record answering "which browser signed in, is it trusted, revoke it". This aggregate
/// answers a different question: "where does a message physically go, and is that place still
/// working". The two have genuinely different lifetimes. A push token rotates without the device
/// changing. A provider rejects a token while the session behind it is perfectly valid. A member
/// revokes a session and the endpoint should die with it, but a member merely signing out on one
/// phone should not lose the endpoint they will use again tomorrow.
/// </para>
/// <para>
/// Modelling the endpoint here rather than reaching into Identity's aggregate keeps ADR-002 intact —
/// this module holds no project reference to Identity at all — and keeps the failure bookkeeping
/// (<see cref="ConsecutiveFailures"/>, <see cref="SuppressedAt"/>) where the failures actually
/// happen. They are correlated by <see cref="DeviceIdentifier"/>, the client's own stable string,
/// so a human debugging one can find the other.
/// </para>
/// <para>
/// <b>The token is stored and never published, logged or returned.</b> It is a credential for
/// somebody else's service. Responses carry <c>hasToken</c>, exactly as Identity's device response
/// carries <c>hasPushToken</c>.
/// </para>
/// </summary>
public sealed class DeviceRegistration : AggregateRoot<DeviceRegistrationId>, IAuditableEntity, ISoftDeletable
{
    /// <summary>
    /// Failures in a row before the endpoint is retired.
    /// <para>
    /// Three, not one: a single push failure is usually the network, and retiring an endpoint on it
    /// would silently unsubscribe members whose train went into a tunnel. Three consecutive is a
    /// token that is genuinely dead.
    /// </para>
    /// </summary>
    public const int FailureTolerance = 3;

    private DeviceRegistration(
        DeviceRegistrationId id,
        UserId userId,
        NotificationChannelKind channel,
        DevicePlatformKind platform,
        string token,
        DateTimeOffset registeredAt) : base(id)
    {
        UserId = userId;
        Channel = channel;
        Platform = platform;
        Token = token;
        RegisteredAt = registeredAt;
        LastActiveAt = registeredAt;
        IsActive = true;
        CreatedAt = registeredAt;
    }

    private DeviceRegistration() { }

    public UserId UserId { get; private set; } = default!;

    /// <summary>Which transport this endpoint serves. <c>Push</c> today; the shape admits others.</summary>
    public NotificationChannelKind Channel { get; private set; }

    public DevicePlatformKind Platform { get; private set; }

    /// <summary>The provider credential. Never leaves this module.</summary>
    public string Token { get; private set; } = string.Empty;

    /// <summary>The client's own stable device string. How Identity's record is correlated.</summary>
    public string? DeviceIdentifier { get; private set; }

    /// <summary>
    /// The session's device, taken from the presented token — B13.3.
    /// <para>
    /// <b>The difference between "this device" as a guess and as an assertion.</b>
    /// <see cref="DeviceIdentifier"/> is a string the client generated and sent, so a client could
    /// claim any of them; the member's own device list showed whichever row matched, and a client
    /// that regenerated its identifier — a reinstall, a cleared store — simply stopped recognising
    /// its own endpoint. This is read from a signed claim the client cannot author, so "this is the
    /// phone in your hand" is something the server knows rather than something it was told.
    /// </para>
    /// <para>
    /// <b>Null is a complete and common answer.</b> A token issued to a caller that named no device
    /// carries no claim, and an endpoint registered before B13.3 has none stored. Both mean "cannot
    /// tell", and the list reports <c>isCurrentDevice: false</c> for them — which is the safe
    /// direction, because the operation this field guards is "sign out everything except this", and
    /// guessing wrong there signs the member out of the device they are holding.
    /// </para>
    /// </summary>
    public Guid? SessionDeviceId { get; private set; }

    /// <summary>What the member calls it. Shown on the devices screen.</summary>
    public string? FriendlyName { get; private set; }

    public string? AppVersion { get; private set; }

    public DateTimeOffset RegisteredAt { get; private set; }

    /// <summary>When the client last checked in. Refreshed on every registration call.</summary>
    public DateTimeOffset LastActiveAt { get; private set; }

    /// <summary>When a message last reached it. The honest measure of whether it works.</summary>
    public DateTimeOffset? LastDeliveredAt { get; private set; }

    public int ConsecutiveFailures { get; private set; }

    /// <summary>When the endpoint stopped being used, and why. Null while it is healthy.</summary>
    public DateTimeOffset? SuppressedAt { get; private set; }

    public string? SuppressionReason { get; private set; }

    public bool IsActive { get; private set; }

    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    /// <summary>Whether the dispatcher should route to it.</summary>
    public bool IsDeliverable => IsActive && !IsDeleted && SuppressedAt is null;

    public static Result<DeviceRegistration> Register(
        DeviceRegistrationId id,
        UserId userId,
        NotificationChannelKind channel,
        DevicePlatformKind platform,
        string token,
        DateTimeOffset registeredAt,
        string? deviceIdentifier = null,
        string? friendlyName = null,
        string? appVersion = null,
        Guid? sessionDeviceId = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(userId);

        if (string.IsNullOrWhiteSpace(token) || token.Trim().Length < 8)
        {
            return Result.Failure<DeviceRegistration>(NotificationErrors.DeviceTokenInvalid);
        }

        var registration = new DeviceRegistration(
            id, userId, channel, platform, token.Trim(), registeredAt)
        {
            DeviceIdentifier = Clip(deviceIdentifier),
            FriendlyName = Clip(friendlyName),
            AppVersion = Clip(appVersion),
            SessionDeviceId = Normalise(sessionDeviceId)
        };

        registration.RaiseDomainEvent(new DeviceRegisteredDomainEvent
        {
            DeviceRegistrationId = id,
            UserId = userId,
            Platform = platform,
            Channel = channel,
            DeviceIdentifier = registration.DeviceIdentifier,
            IsNewRegistration = true,
            RegisteredAt = registeredAt
        });

        return Result.Success(registration);
    }

    /// <summary>
    /// The same endpoint checking in again, usually with a rotated token.
    /// <para>
    /// Refreshing rather than replacing is what keeps a member's device list from growing a new row
    /// every time the OS reissues a token — which on iOS is routine, and would otherwise leave a
    /// year-old account with forty dead endpoints and one live one.
    /// </para>
    /// <para>
    /// A refresh clears the failure count and any suppression: a client that has just handed us a
    /// token is, by construction, working.
    /// </para>
    /// </summary>
    /// <param name="sessionDeviceId">
    /// The session's device, from the token presenting this refresh — B13.3.
    /// <para>
    /// <b>Overwritten rather than merged, and null does not clear it.</b> A token restored onto a new
    /// phone genuinely belongs to that phone now, so the newest claim is the right answer. A refresh
    /// arriving on a token that names no device says nothing about which device holds the endpoint,
    /// and treating silence as a correction would erase a good answer with no answer — after which
    /// "sign out my other devices" would sign the member out of the one they are holding.
    /// </para>
    /// </param>
    public Result Refresh(
        string token,
        DateTimeOffset now,
        DevicePlatformKind? platform = null,
        string? friendlyName = null,
        string? appVersion = null,
        Guid? sessionDeviceId = null)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Trim().Length < 8)
        {
            return Result.Failure(NotificationErrors.DeviceTokenInvalid);
        }

        if (Normalise(sessionDeviceId) is { } session)
        {
            SessionDeviceId = session;
        }

        Token = token.Trim();
        LastActiveAt = now;
        ConsecutiveFailures = 0;
        SuppressedAt = null;
        SuppressionReason = null;
        IsActive = true;

        if (platform is { } value && value != DevicePlatformKind.Unknown)
        {
            Platform = value;
        }

        if (!string.IsNullOrWhiteSpace(friendlyName))
        {
            FriendlyName = Clip(friendlyName);
        }

        if (!string.IsNullOrWhiteSpace(appVersion))
        {
            AppVersion = Clip(appVersion);
        }

        Touch();

        RaiseDomainEvent(new DeviceRegisteredDomainEvent
        {
            DeviceRegistrationId = Id,
            UserId = UserId,
            Platform = Platform,
            Channel = Channel,
            DeviceIdentifier = DeviceIdentifier,
            IsNewRegistration = false,
            RegisteredAt = now
        });

        return Result.Success();
    }

    /// <summary>A message reached it. Resets the failure count.</summary>
    public void RecordDelivery(DateTimeOffset now)
    {
        LastDeliveredAt = now;
        ConsecutiveFailures = 0;

        Touch();
    }

    /// <summary>
    /// A message did not reach it.
    /// <para>
    /// Returns true when the endpoint has been retired as a result, so the caller knows to publish
    /// nothing further to it. A permanent rejection retires immediately; anything else needs
    /// <see cref="FailureTolerance"/> in a row.
    /// </para>
    /// </summary>
    public bool RecordFailure(string reasonCode, bool isPermanent, DateTimeOffset now)
    {
        ConsecutiveFailures++;

        if (!isPermanent && ConsecutiveFailures < FailureTolerance)
        {
            Touch();
            return false;
        }

        SuppressedAt = now;
        SuppressionReason = Clip(reasonCode);
        IsActive = false;

        Touch();

        RaiseDomainEvent(new DeviceRetiredDomainEvent
        {
            DeviceRegistrationId = Id,
            UserId = UserId,
            Reason = isPermanent ? "Rejected" : "TooManyFailures",
            RetiredAt = now
        });

        return true;
    }

    /// <summary>The member removed it. Soft-deleted rather than switched off, so it stops being listed.</summary>
    public void Remove(DateTimeOffset now)
    {
        IsActive = false;
        SuppressedAt = now;
        SuppressionReason = "Removed";

        Touch();

        RaiseDomainEvent(new DeviceRetiredDomainEvent
        {
            DeviceRegistrationId = Id,
            UserId = UserId,
            Reason = "Removed",
            RetiredAt = now
        });
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

    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        IsActive = false;
        DeletedAt = timestamp;
        DeletedBy = userId;
    }

    /// <summary>
    /// Undeletes. Distinct from <see cref="Refresh"/>, which is the ordinary way an endpoint comes
    /// back. Callers must load the row with the global query filter disabled.
    /// </summary>
    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;

        Touch();
    }

    /// <summary>
    /// Whether this endpoint is the one the caller is speaking from.
    /// <para>
    /// <b>Both sides must be known.</b> An unknown session compared against an unrecorded device is
    /// two nulls, and answering true for that would tell every member that every endpoint they have
    /// ever registered is the phone in their hand.
    /// </para>
    /// </summary>
    public bool IsCurrentFor(Guid? sessionDeviceId) =>
        Normalise(sessionDeviceId) is { } session && SessionDeviceId == session;

    /// <summary>The empty identifier is not an identifier. Treated as absent everywhere.</summary>
    private static Guid? Normalise(Guid? value) =>
        value is { } parsed && parsed != Guid.Empty ? parsed : null;

    private static string? Clip(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= NotificationLimits.MaxKeyLength
            ? trimmed
            : trimmed[..NotificationLimits.MaxKeyLength];
    }

    private void Touch() => Revision++;
}
