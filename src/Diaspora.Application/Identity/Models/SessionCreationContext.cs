using Diaspora.Domain.Identity.Devices;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Application.Identity.Models;

/// <summary>
/// Device and network details captured when a session opens, so the member's session
/// list reads as "Pixel 8, Lagos, 2 minutes ago" rather than as a row of opaque ids.
/// </summary>
public sealed record SessionCreationContext
{
    public DeviceId? DeviceId { get; init; }

    public string? DeviceIdentifier { get; init; }

    public string? DeviceName { get; init; }

    public DevicePlatform Platform { get; init; } = DevicePlatform.Unknown;

    public string? AppVersion { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }
}
