using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Application.Mapping;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Devices;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Devices;

/// <summary>
/// The caller's endpoints.
/// <para>
/// Suppressed endpoints are included and say why. A devices screen that hid them would leave a
/// member wondering where their tablet went, and an operator with nothing to point at.
/// </para>
/// </summary>
public sealed class ListNotificationDevicesQueryHandler
    : IQueryHandler<ListNotificationDevicesQuery, IReadOnlyList<DeviceRegistrationResponse>>
{
    private readonly IDeviceRegistrationRepository _devices;
    private readonly NotificationAuthorizationService _authorization;

    public ListNotificationDevicesQueryHandler(
        IDeviceRegistrationRepository devices, NotificationAuthorizationService authorization)
    {
        _devices = devices;
        _authorization = authorization;
    }

    public async Task<Result<IReadOnlyList<DeviceRegistrationResponse>>> Handle(
        ListNotificationDevicesQuery query, CancellationToken cancellationToken)
    {
        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<DeviceRegistrationResponse>>(caller.Error);
        }

        var devices = await _devices.ListForUserAsync(caller.Value, cancellationToken);
        var session = _authorization.SessionDeviceId;

        return Result.Success<IReadOnlyList<DeviceRegistrationResponse>>(
            devices.Select(device => DeviceMapper.ToResponse(device, session)).ToList());
    }
}
