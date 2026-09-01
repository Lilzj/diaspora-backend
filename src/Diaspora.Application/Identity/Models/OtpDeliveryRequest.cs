using Diaspora.Domain.Identity.Verification;

namespace Diaspora.Application.Identity.Models;

/// <summary>
/// Instruction to deliver a one-time code. Carries the plaintext code, which is the only
/// place in the system outside the HTTP response where it exists.
/// </summary>
public sealed record OtpDeliveryRequest
{
    public required string PhoneNumber { get; init; }

    /// <summary>
    /// The member's registered email address, when they have one.
    /// </summary>
    /// <remarks>
    /// Carried alongside the phone number rather than instead of it, because the transport — not
    /// the use case — decides which one a given purpose travels on. A phone-verification code has
    /// to go to the phone being verified; a recovery code does not, and sending it by email is
    /// the whole reason sign-in stopped costing an SMS.
    /// </remarks>
    public string? EmailAddress { get; init; }

    public required string Code { get; init; }

    public required OtpPurpose Purpose { get; init; }

    /// <summary>BCP 47 tag for the member's preferred language.</summary>
    public required string Language { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}
