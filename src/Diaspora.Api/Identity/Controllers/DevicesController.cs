using Asp.Versioning;
using Diaspora.Api.Common;
using Diaspora.Platform.Http;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Application.Identity.Commands.Devices.RegisterDevice;
using Diaspora.Application.Identity.Commands.Devices.UpdatePushToken;
using Diaspora.Application.Identity.Queries.GetDevices;
using Diaspora.Contracts.Identity.V1.Requests;
using Diaspora.Contracts.Identity.V1.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diaspora.Api.Identity.Controllers;

/// <summary>
/// Device registration and push-token management.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiEndpoints.BaseUrl + "/auth/devices")]
[Produces("application/json")]
[Authorize(Policy = DiasporaPolicies.ManageOwnDevices)]
public sealed class DevicesController : ApiController
{
    private readonly ISender _sender;

    public DevicesController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Lists the devices registered to the member.</summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<DeviceResponse>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDevicesAsync(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetDevicesQuery(), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Registers or refreshes a device. Idempotent on the device identifier.</summary>
    [HttpPost]
    [ProducesResponseType<ApiResponse<DeviceResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterDeviceAsync(
        [FromBody] RegisterDeviceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _sender.Send(new RegisterDeviceCommand
        {
            DeviceIdentifier = request.DeviceIdentifier,
            FriendlyName = request.FriendlyName,
            Platform = request.Platform,
            AppVersion = request.AppVersion,
            PushToken = request.PushToken
        }, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>Replaces a device's push registration token. Send null to clear it.</summary>
    [HttpPut("{deviceId:guid}/push-token")]
    [ProducesResponseType<ApiResponse<DeviceResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePushTokenAsync(
        Guid deviceId,
        [FromBody] UpdatePushTokenRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _sender.Send(new UpdatePushTokenCommand
        {
            DeviceId = deviceId,
            PushToken = request.PushToken
        }, cancellationToken);

        return HandleResult(result);
    }
}
