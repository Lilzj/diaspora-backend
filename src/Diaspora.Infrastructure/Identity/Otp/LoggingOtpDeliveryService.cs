using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.Verification;
using Diaspora.Infrastructure.Identity.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Infrastructure.Identity.Otp;

/// <summary>
/// Development-time OTP delivery.
/// <para>
/// Stands in for an SMS gateway so the full registration and recovery flows are runnable
/// without a provider account. The code is written to the log only when
/// <c>Identity:LogOtpCodesForDevelopment</c> is enabled, and the startup validator refuses
/// to boot outside Development with that flag set — a log full of live codes is a
/// credential store that nobody is guarding as one.
/// </para>
/// <para>
/// Replacing this with a real gateway is a single DI registration; no use case changes.
/// </para>
/// </summary>
public sealed class LoggingOtpDeliveryService : IOtpDeliveryService
{
    private readonly IdentityModuleOptions _options;
    private readonly ILogger<LoggingOtpDeliveryService> _logger;

    public LoggingOtpDeliveryService(
        IOptions<IdentityModuleOptions> options,
        ILogger<LoggingOtpDeliveryService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _logger = logger;
    }

    public Task<bool> SendAsync(OtpDeliveryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_options.LogOtpCodesForDevelopment)
        {
            _logger.LogWarning(
                "DEVELOPMENT ONLY - {Purpose} code for {PhoneNumber} is {Code}, valid until {ExpiresAt:u}",
                request.Purpose, request.PhoneNumber, request.Code, request.ExpiresAt);

            // The code reached somebody who can read it. For a development host, that is delivery.
            return Task.FromResult(true);
        }

        // ── It used to say "Dispatched" here, and return true ────────────────────────────
        //
        // Nothing was dispatched. This type has never had a transport; the line was written when
        // an SMS gateway was expected to arrive behind the same port, and until one did, every
        // deployment without `LogOtpCodesForDevelopment` reported successful delivery of codes
        // that were never sent.
        //
        // What that cost: on the hosted deployment nobody could sign in through the app, because
        // the only path in is an SMS code — and the API's own logs said delivery was working, so
        // the deployment looked healthy while being unusable by any real member.
        //
        // False is the truth, and `OtpChallengeService` already knows what to do with it: the
        // member is told the code could not be sent, which is a screen they can act on rather
        // than a wait that never ends.
        _logger.LogError(
            "No SMS provider is configured, so a {Purpose} code for {PhoneNumber} was not sent. "
            + "Configure the 'Sms' section — see Diaspora.Sms — or enable "
            + "'Identity:LogOtpCodesForDevelopment' on a development host.",
            request.Purpose, Mask(request.PhoneNumber));

        return Task.FromResult(false);
    }

    /// <summary>
    /// SMS when the development log is standing in for a gateway, and nothing otherwise.
    /// </summary>
    /// <remarks>
    /// The honest answer for a host with no provider is <see cref="OtpDeliveryChannel.None"/>,
    /// which is what has recovery tell the member no code could be sent rather than name a
    /// channel nothing will arrive on.
    /// </remarks>
    public OtpDeliveryChannel ChannelFor(OtpPurpose purpose) =>
        _options.LogOtpCodesForDevelopment ? OtpDeliveryChannel.Sms : OtpDeliveryChannel.None;

    private static string Mask(string phoneNumber)
    {
        if (phoneNumber.Length <= 6) return "***";
        return $"{phoneNumber[..3]}***{phoneNumber[^3..]}";
    }
}
