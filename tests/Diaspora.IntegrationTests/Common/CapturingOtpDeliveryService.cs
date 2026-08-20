using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;

namespace Diaspora.IntegrationTests.Common;

/// <summary>
/// Test stand-in for the SMS gateway. Records the code instead of sending it.
/// </summary>
public sealed class CapturingOtpDeliveryService : IOtpDeliveryService
{
    private readonly CapturedOtpCodes _captured;

    public CapturingOtpDeliveryService(CapturedOtpCodes captured)
    {
        _captured = captured;
    }

    public Task<bool> SendAsync(OtpDeliveryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _captured.Record(request.PhoneNumber, request.Purpose, request.Code);
        return Task.FromResult(true);
    }
}
