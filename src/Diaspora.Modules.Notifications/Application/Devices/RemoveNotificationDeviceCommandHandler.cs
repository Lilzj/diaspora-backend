using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Domain.Devices;
using Diaspora.Modules.Notifications.Domain.Errors;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Devices;

/// <summary>
/// The member removed a device.
/// <para>
/// The row stays and stops being deliverable, rather than being deleted. A member asking "why did my
/// old phone stop getting notifications" deserves an answer, and an endpoint that vanished leaves
/// nobody able to give one.
/// </para>
/// <para>
/// An endpoint belonging to somebody else is reported as not found, for the same reason a
/// notification is: distinguishing the two would let anybody learn which identifiers exist.
/// </para>
/// </summary>
public sealed class RemoveNotificationDeviceCommandHandler
    : ICommandHandler<RemoveNotificationDeviceCommand>
{
    private readonly IDeviceRegistrationRepository _devices;
    private readonly NotificationAuthorizationService _authorization;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public RemoveNotificationDeviceCommandHandler(
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

    public async Task<Result> Handle(
        RemoveNotificationDeviceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        var device = await _devices.GetAsync(
            DeviceRegistrationId.From(command.DeviceId), cancellationToken);

        if (device is null || device.UserId != caller.Value)
        {
            return Result.Failure(NotificationErrors.DeviceNotFound);
        }

        device.Remove(_clock.UtcNow);

        _devices.Update(device);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
