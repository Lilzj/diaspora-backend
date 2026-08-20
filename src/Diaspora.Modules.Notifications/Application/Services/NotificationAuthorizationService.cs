using Diaspora.Contracts.Authorization;
using Diaspora.Modules.Notifications.Domain.Errors;
using Diaspora.Modules.Notifications.Domain.Notifications;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Services;

/// <summary>
/// Who the caller is, and whether a message is theirs.
/// <para>
/// <b>Ownership in this module is simpler than in any other vertical and stricter.</b> There is no
/// sharing, no visibility tier and no public read: a notification belongs to exactly one member, and
/// nobody else may read it — not a moderator, not an administrator. What staff may read is the
/// <em>delivery record</em> — did it go, did it fail, how many times — which names no words and no
/// subject. That distinction is enforced here rather than left to each handler.
/// </para>
/// <para>
/// <b>A message belonging to somebody else is reported as not found.</b> Distinguishing the two would
/// let anybody enumerate identifiers and learn which ones exist, which for notification identifiers
/// means learning that a given member was told something.
/// </para>
/// </summary>
public sealed class NotificationAuthorizationService
{
    private static readonly HashSet<string> StaffRoles =
        new(StringComparer.Ordinal) { "Moderator", "Admin", "SuperAdmin" };

    private readonly ICurrentUser _currentUser;

    public NotificationAuthorizationService(ICurrentUser currentUser) => _currentUser = currentUser;

    /// <summary>The signed-in member, or a failure that becomes a 401.</summary>
    public Result<UserId> RequireCaller() =>
        _currentUser is { IsAuthenticated: true, UserId: { } userId }
            ? Result.Success(userId)
            : Result.Failure<UserId>(NotificationErrors.Unauthenticated);

    public bool IsStaff =>
        _currentUser.Role is { } role && StaffRoles.Contains(role);

    /// <summary>
    /// The device the caller's token was issued for, or null when it names none — B13.3.
    /// <para>
    /// <b>Read from a signed claim, which is the entire point.</b> The device list used to decide
    /// which row was "this one" by matching a string the client generated and sent; a client could
    /// therefore claim any of them, and an honest client that regenerated its identifier lost track
    /// of its own. A claim inside the access token cannot be authored by the caller, so this turns a
    /// guess into an assertion — and the operation it guards is "deactivate everything except the
    /// device I am holding", where being wrong logs the member out of the phone in their hand.
    /// </para>
    /// <para>
    /// Null on a token that named no device, which is a complete answer meaning "cannot tell". Every
    /// caller must treat it as such rather than as "no device matches".
    /// </para>
    /// </summary>
    public Guid? SessionDeviceId =>
        _currentUser.Claims.TryGetValue(PlatformClaimTypes.DeviceId, out var raw)
        && Guid.TryParse(raw, out var parsed)
        && parsed != Guid.Empty
            ? parsed
            : null;

    /// <summary>
    /// The message, if it is the caller's.
    /// <para>
    /// Takes the loaded aggregate rather than an identifier so a handler cannot forget to check
    /// after loading — the only way to get past this method is to have already been given the thing
    /// it is guarding.
    /// </para>
    /// </summary>
    public static Result<Notification> RequireOwnership(Notification? notification, UserId caller)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (notification is null)
        {
            return Result.Failure<Notification>(NotificationErrors.NotificationNotFound);
        }

        return notification.RecipientUserId == caller
            ? Result.Success(notification)
            : Result.Failure<Notification>(NotificationErrors.NotificationNotOwned);
    }

    /// <summary>Staff-only surfaces: template authoring, delivery history, analytics, retries.</summary>
    public Result RequireStaff() =>
        IsStaff ? Result.Success() : Result.Failure(NotificationErrors.Forbidden);
}
