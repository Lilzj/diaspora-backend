using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
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
        }
        else
        {
            _logger.LogInformation(
                "Dispatched a {Purpose} code to {PhoneNumber} in {Language}, valid until {ExpiresAt:u}",
                request.Purpose, Mask(request.PhoneNumber), request.Language, request.ExpiresAt);
        }

        return Task.FromResult(true);
    }

    private static string Mask(string phoneNumber)
    {
        if (phoneNumber.Length <= 6) return "***";
        return $"{phoneNumber[..3]}***{phoneNumber[^3..]}";
    }
}
