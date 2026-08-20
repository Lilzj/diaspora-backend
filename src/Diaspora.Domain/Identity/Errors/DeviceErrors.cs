using Diaspora.SharedKernel.Results;

namespace Diaspora.Domain.Identity.Errors;

/// <summary>
/// Typed failures raised by the <c>UserDevice</c> aggregate.
/// </summary>
public static class DeviceErrors
{
    public static readonly Error NotFound =
        Error.NotFound("Identity.Device.NotFound", "The requested device could not be found.");

    public static readonly Error NotOwnedByUser =
        Error.Forbidden("Identity.Device.NotOwnedByUser", "The device does not belong to the current account.");

    public static readonly Error LimitReached =
        Error.Conflict("Identity.Device.LimitReached", "The maximum number of registered devices has been reached.");
}
