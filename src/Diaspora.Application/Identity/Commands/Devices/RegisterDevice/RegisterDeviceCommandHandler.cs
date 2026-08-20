using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Mapping;
using Diaspora.Application.Identity.Validation;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.Devices;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Commands.Devices.RegisterDevice;

public sealed class RegisterDeviceCommandHandler : ICommandHandler<RegisterDeviceCommand, DeviceResponse>
{
    private readonly IUserDeviceRepository _devices;
    private readonly IIdentityPolicyProvider _policies;
    private readonly ICurrentIdentityContext _currentIdentity;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public RegisterDeviceCommandHandler(
        IUserDeviceRepository devices,
        IIdentityPolicyProvider policies,
        ICurrentIdentityContext currentIdentity,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _devices = devices;
        _policies = policies;
        _currentIdentity = currentIdentity;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<DeviceResponse>> Handle(
        RegisterDeviceCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentIdentity.UserId;
        if (userId is null)
        {
            return Result.Failure<DeviceResponse>(UserErrors.InvalidCredentials);
        }

        var now = _clock.UtcNow;
        var platform = IdentityValueParser.ParsePlatform(request.Platform);
        var identifier = request.DeviceIdentifier.Trim();

        var device = await _devices.GetByIdentifierAsync(userId, identifier, cancellationToken);

        if (device is null)
        {
            var registered = await _devices.CountByUserAsync(userId, cancellationToken);
            if (registered >= _policies.Current.MaxDevicesPerUser)
            {
                // Unlike sign-in, an explicit registration call should say so rather than
                // silently do nothing — the client asked a direct question.
                return Result.Failure<DeviceResponse>(DeviceErrors.LimitReached);
            }

            device = UserDevice.Register(
                userId, identifier, request.FriendlyName ?? string.Empty, platform, request.AppVersion, now);

            await _devices.AddAsync(device, cancellationToken);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.FriendlyName))
            {
                var rename = device.Rename(request.FriendlyName);
                if (rename.IsFailure)
                {
                    return Result.Failure<DeviceResponse>(rename.Error);
                }
            }

            device.RecordActivity(now, request.AppVersion);
            _devices.Update(device);
        }

        if (request.PushToken is not null)
        {
            var pushUpdate = device.UpdatePushToken(request.PushToken);
            if (pushUpdate.IsFailure)
            {
                return Result.Failure<DeviceResponse>(pushUpdate.Error);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(DeviceMapper.ToResponse(device));
    }
}
