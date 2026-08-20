using Diaspora.Domain.Identity.Verification;

namespace Diaspora.Application.Identity.Models;

/// <summary>
/// Instruction to deliver a one-time code. Carries the plaintext code, which is the only
/// place in the system outside the HTTP response where it exists.
/// </summary>
public sealed record OtpDeliveryRequest
{
    public required string PhoneNumber { get; init; }

    public required string Code { get; init; }

    public required OtpPurpose Purpose { get; init; }

    /// <summary>BCP 47 tag for the member's preferred language.</summary>
    public required string Language { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}
