using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Domain.Devices;
using Diaspora.Modules.Notifications.Domain.Errors;
using Diaspora.Modules.Notifications.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Devices;

/// <summary>
/// Stop delivering to everything except the device making the request.
/// <para>
/// <b>The one operation on this surface that takes no identifier, deliberately.</b> "Sign my other
/// devices out" is a thing a member does when they have lost a phone or shared a screen, and the one
/// device they can name reliably is the one in their hand — which they should not have to name,
/// because a client that named the wrong one would silence the device its owner is holding and leave
/// the lost one receiving.
/// </para>
/// <para>
/// This is delivery, not sessions. Identity owns "revoke that session"; this owns "stop pushing
/// there". A member who has lost a phone wants both, and the client calls both, but they are
/// different records with different lifetimes and the platform has never conflated them.
/// </para>
/// </summary>
[RequirePermission(NotificationPermissions.DevicesManage)]
[Transactional(Reason = "Retires every other endpoint in one unit of work, or none of them.")]
[Auditable]
public sealed record DeactivateOtherNotificationDevicesCommand : ICommand<int>;

/// <summary>
/// Retires every deliverable endpoint the caller holds except the one the request came from.
/// <para>
/// <b>Refused outright when the token names no device.</b> There is no safe interpretation of "all
/// except this one" when the server cannot tell which one "this" is: retiring everything would
/// silence the member's own phone with no way to get it back except a re-registration they have no
/// reason to know they need. A failure the client can explain beats an action nobody asked for.
/// </para>
/// </summary>
public sealed class DeactivateOtherNotificationDevicesCommandHandler
    : ICommandHandler<DeactivateOtherNotificationDevicesCommand, int>
{
    private readonly IDeviceRegistrationRepository _devices;
    private readonly NotificationAuthorizationService _authorization;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public DeactivateOtherNotificationDevicesCommandHandler(
        IDeviceRegistrationRepository devices,
        NotificationAuthorizationService authorization,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _devices = devices;
        _authorization = authorization;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<int>> Handle(
        DeactivateOtherNotificationDevicesCommand command, CancellationToken cancellationToken)
    {
        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<int>(caller.Error);
        }

        if (_authorization.SessionDeviceId is not { } session)
        {
            return Result.Failure<int>(NotificationErrors.DeviceSessionUnknown);
        }

        // Tracked, and deliverable only. The devices screen's list is untracked and includes retired
        // rows, which is right for a read and wrong here twice over: re-attaching an untracked
        // aggregate for a write is refused as a concurrency conflict, and re-retiring an endpoint
        // that stopped working last March would put a fictitious entry in the member's own delivery
        // history.
        var devices = await _devices.ListDeliverableForUpdateAsync(caller.Value, cancellationToken);
        var now = _clock.UtcNow;
        var retired = 0;

        foreach (var device in devices)
        {
            if (device.IsCurrentFor(session))
            {
                continue;
            }

            device.Remove(now);
            _devices.Update(device);
            retired++;
        }

        if (retired == 0)
        {
            return Result.Success(0);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        NotificationTelemetry.DevicesRetired.Add(
            retired, new KeyValuePair<string, object?>("reason", "DeactivatedByMember"));

        return Result.Success(retired);
    }
}
