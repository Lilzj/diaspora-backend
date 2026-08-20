using Diaspora.Domain.Identity.Devices;
using Diaspora.Domain.Identity.Trust;
using Diaspora.Domain.Identity.Verification;

namespace Diaspora.Application.Identity.Validation;

/// <summary>
/// Parses the loosely typed strings that arrive on the wire into domain enums.
/// <para>
/// Shared between validators and handlers so a value can never pass validation under one
/// interpretation and be parsed differently a moment later.
/// </para>
/// </summary>
public static class IdentityValueParser
{
    /// <summary>
    /// Parses a client platform. Unrecognised or absent values become
    /// <see cref="DevicePlatform.Unknown"/> rather than failing — a mislabelled device
    /// is not a reason to refuse someone a sign-in.
    /// </summary>
    public static DevicePlatform ParsePlatform(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return DevicePlatform.Unknown;

        return value.Trim().ToLowerInvariant() switch
        {
            "android" => DevicePlatform.Android,
            "ios" => DevicePlatform.IOS,
            "web" => DevicePlatform.Web,
            "tablet" => DevicePlatform.Tablet,
            _ => DevicePlatform.Unknown
        };
    }

    public static bool IsKnownPlatform(string? value) =>
        !string.IsNullOrWhiteSpace(value) && ParsePlatform(value) != DevicePlatform.Unknown;

    /// <summary>
    /// Parses an OTP purpose. Unlike platform, an unrecognised value must fail: redeeming
    /// a code against the wrong purpose is a security boundary, not a cosmetic detail.
    /// </summary>
    public static bool TryParseOtpPurpose(string? value, out OtpPurpose purpose)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            purpose = OtpPurpose.PhoneVerification;
            return true;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "phoneverification":
            case "phone_verification":
                purpose = OtpPurpose.PhoneVerification;
                return true;
            case "passwordreset":
            case "password_reset":
                purpose = OtpPurpose.PasswordReset;
                return true;
            case "accountrecovery":
            case "account_recovery":
                purpose = OtpPurpose.AccountRecovery;
                return true;
            case "stepupauthentication":
            case "step_up":
                purpose = OtpPurpose.StepUpAuthentication;
                return true;
            default:
                purpose = OtpPurpose.PhoneVerification;
                return false;
        }
    }

    public static bool TryParseTrustOutcome(string? value, out TrustDecisionOutcome outcome)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "underreview":
            case "under_review":
                outcome = TrustDecisionOutcome.UnderReview;
                return true;
            case "granted":
                outcome = TrustDecisionOutcome.Granted;
                return true;
            case "rejected":
                outcome = TrustDecisionOutcome.Rejected;
                return true;
            case "revoked":
                outcome = TrustDecisionOutcome.Revoked;
                return true;
            default:
                outcome = TrustDecisionOutcome.UnderReview;
                return false;
        }
    }

    /// <summary>Renders a platform back to the wire vocabulary.</summary>
    public static string Format(DevicePlatform platform) => platform switch
    {
        DevicePlatform.Android => "Android",
        DevicePlatform.IOS => "IOS",
        DevicePlatform.Web => "Web",
        DevicePlatform.Tablet => "Tablet",
        _ => "Unknown"
    };
}
