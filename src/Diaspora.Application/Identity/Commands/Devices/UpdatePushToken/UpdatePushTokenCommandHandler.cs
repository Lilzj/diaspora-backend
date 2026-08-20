using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Mapping;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Commands.Devices.UpdatePushToken;

public sealed class UpdatePushTokenCommandHandler : ICommandHandler<UpdatePushTokenCommand, DeviceResponse>
{
    private readonly IUserDeviceRepository _devices;
    private readonly ICurrentIdentityContext _currentIdentity;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public UpdatePushTokenCommandHandler(
        IUserDeviceRepository devices,
        ICurrentIdentityContext currentIdentity,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _devices = devices;
        _currentIdentity = currentIdentity;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<DeviceResponse>> Handle(
        UpdatePushTokenCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentIdentity.UserId;
        if (userId is null)
        {
            return Result.Failure<DeviceResponse>(UserErrors.InvalidCredentials);
        }

        var device = await _devices.GetByIdAsync(DeviceId.From(request.DeviceId), cancellationToken);

        // Another member's device reports as missing, not as forbidden: confirming it
        // exists would leak the identifier space.
        if (device is null || device.UserId != userId)
        {
            return Result.Failure<DeviceResponse>(DeviceErrors.NotFound);
        }

        var update = device.UpdatePushToken(request.PushToken);
        if (update.IsFailure)
        {
            return Result.Failure<DeviceResponse>(update.Error);
        }

        device.RecordActivity(_clock.UtcNow);
        _devices.Update(device);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(DeviceMapper.ToResponse(device));
    }
}
