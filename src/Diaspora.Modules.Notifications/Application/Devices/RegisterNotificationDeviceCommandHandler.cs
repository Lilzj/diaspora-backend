using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Notifications.Application.Mapping;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Devices;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Errors;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.Modules.Notifications.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Notifications.Application.Devices;

/// <summary>
/// Registers or refreshes a push endpoint.
/// <para>
/// <b>Looked up by token, not by device.</b> A token can migrate between devices — a backup restored
/// onto a new phone — and registering it a second time would push every message twice. Finding the
/// existing endpoint and refreshing it is also what keeps a year-old account from accumulating forty
/// dead registrations, because iOS reissues tokens routinely.
/// </para>
/// </summary>
public sealed class RegisterNotificationDeviceCommandHandler
    : ICommandHandler<RegisterNotificationDeviceCommand, DeviceRegistrationResponse>
{
    private readonly IDeviceRegistrationRepository _devices;
    private readonly NotificationAuthorizationService _authorization;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOptions<NotificationOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public RegisterNotificationDeviceCommandHandler(
        IDeviceRegistrationRepository devices,
        NotificationAuthorizationService authorization,
        IUnitOfWork unitOfWork,
        IOptions<NotificationOptions> options,
        IGuidGenerator guids,
        IClock clock)
    {
        _devices = devices;
        _authorization = authorization;
        _unitOfWork = unitOfWork;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<DeviceRegistrationResponse>> Handle(
        RegisterNotificationDeviceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<DeviceRegistrationResponse>(caller.Error);
        }

        var request = command.Request;
        var platform = NotificationMapper.ParseEnum<DevicePlatformKind>(request.Platform)
                       ?? DevicePlatformKind.Unknown;

        // B13.3. The device the caller's own token names. Recorded rather than accepted from the
        // request body: the client already sends a DeviceIdentifier it made up, and a member cannot
        // be protected by a value the caller chose. See NotificationAuthorizationService.
        var session = _authorization.SessionDeviceId;

        var existing = await _devices.FindByTokenAsync(caller.Value, request.Token, cancellationToken);

        if (existing is not null)
        {
            var refreshed = existing.Refresh(
                request.Token,
                _clock.UtcNow,
                platform,
                request.FriendlyName,
                request.AppVersion,
                session);

            if (refreshed.IsFailure)
            {
                return Result.Failure<DeviceRegistrationResponse>(refreshed.Error);
            }

            _devices.Update(existing);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(DeviceMapper.ToResponse(existing, session));
        }

        var active = await _devices.CountActiveAsync(caller.Value, cancellationToken);

        if (active >= _options.Value.MaxDevicesPerUser)
        {
            return Result.Failure<DeviceRegistrationResponse>(NotificationErrors.DeviceLimitReached);
        }

        var registration = DeviceRegistration.Register(
            DeviceRegistrationId.From(_guids.NewSequentialGuid()),
            caller.Value,
            NotificationChannelKind.Push,
            platform,
            request.Token,
            _clock.UtcNow,
            request.DeviceIdentifier,
            request.FriendlyName,
            request.AppVersion,
            session);

        if (registration.IsFailure)
        {
            return Result.Failure<DeviceRegistrationResponse>(registration.Error);
        }

        await _devices.AddAsync(registration.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(DeviceMapper.ToResponse(registration.Value, session));
    }
}
