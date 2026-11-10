using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Queries.GetDevices;

/// <summary>Lists the devices registered to the signed-in member.</summary>
[RequirePermission("identity:devices:manage")]
public sealed record GetDevicesQuery : IQuery<IReadOnlyList<DeviceResponse>>;
