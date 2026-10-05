using Diaspora.SharedKernel.Identity;

namespace Diaspora.Application.Identity.Abstractions;

/// <summary>
/// Signs and reads email verification links.
/// <para>
/// <b>Signed, not stored.</b> A link carries the member, the exact address it was sent to and an
/// expiry, under a signature only the platform can make. That gives every property the
/// verification contract asks for without a table: it cannot be forged, it expires in hours, and
/// it is worthless the moment the address on the account changes, because confirming compares the
/// address in the link with the address on the account. Opening it twice is harmless, because the
/// second confirmation changes nothing.
/// </para>
/// </summary>
public interface IEmailVerificationLinks
{
    /// <summary>Mints a token for <paramref name="address"/>, valid until the returned instant.</summary>
    (string Token, DateTimeOffset ExpiresAt) Create(UserId userId, string address);

    /// <summary>Reads a token. Null when it is malformed, forged or expired. Never throws.</summary>
    EmailVerificationClaim? Redeem(string? token);
}

public sealed record EmailVerificationClaim(UserId UserId, string Address);
