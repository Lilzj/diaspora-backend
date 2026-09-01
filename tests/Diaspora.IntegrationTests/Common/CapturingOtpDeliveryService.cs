using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.Verification;

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

    /// <summary>
    /// SMS, for every purpose.
    /// </summary>
    /// <remarks>
    /// This stands in for the transport itself, so it answers as a deployment with SMS and no
    /// email would. Recovery's acknowledgement is asserted against that in the integration
    /// suite; a test that wants the email wording configures the email adapter instead of
    /// changing this.
    /// </remarks>
    public OtpDeliveryChannel ChannelFor(OtpPurpose purpose) => OtpDeliveryChannel.Sms;
}
