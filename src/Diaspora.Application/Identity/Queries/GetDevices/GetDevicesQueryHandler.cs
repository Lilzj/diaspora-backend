using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Mapping;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Queries.GetDevices;

public sealed class GetDevicesQueryHandler : IQueryHandler<GetDevicesQuery, IReadOnlyList<DeviceResponse>>
{
    private readonly IUserDeviceRepository _devices;
    private readonly ICurrentIdentityContext _currentIdentity;

    public GetDevicesQueryHandler(IUserDeviceRepository devices, ICurrentIdentityContext currentIdentity)
    {
        _devices = devices;
        _currentIdentity = currentIdentity;
    }

    public async Task<Result<IReadOnlyList<DeviceResponse>>> Handle(
        GetDevicesQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentIdentity.UserId;
        if (userId is null)
        {
            return Result.Failure<IReadOnlyList<DeviceResponse>>(UserErrors.InvalidCredentials);
        }

        var devices = await _devices.ListByUserAsync(userId, cancellationToken);

        return Result.Success(DeviceMapper.ToResponses(
            devices.OrderByDescending(device => device.LastActiveAt)));
    }
}
