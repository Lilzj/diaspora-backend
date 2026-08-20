using Diaspora.Contracts.Authorization;

namespace Diaspora.Infrastructure.Identity.Tokens;

/// <summary>
/// The claim names Diaspora access tokens carry beyond the registered JWT set.
/// <para>
/// Short names on purpose. Every one of these rides on every authenticated request, and
/// the URI-style claim names ASP.NET Core defaults to would add hundreds of bytes per
/// call for no benefit — costly over the mobile connections much of Diaspora's audience
/// is on.
/// </para>
/// </summary>
public static class DiasporaClaimTypes
{
    /// <summary>The session the token was issued for. Enables per-session revocation.</summary>
    public const string SessionId = "sid";

    /// <summary>
    /// The device the session belongs to, when one was reported.
    /// <para>
    /// Spelled once, in Contracts, since B13.3 — the Communication Platform reads it to decide which
    /// of a member's push endpoints is the one making the request, and a second literal here would be
    /// two strings that have to stay equal with nothing checking them.
    /// </para>
    /// </summary>
    public const string DeviceId = PlatformClaimTypes.DeviceId;

    /// <summary>The member's position on the progressive access ladder at issuance.</summary>
    public const string AccessState = "acs";

    /// <summary>
    /// The member's platform access tier at issuance. Read by the kernel's tier
    /// authorisation handler, which knows nothing about <see cref="AccessState"/>.
    /// </summary>
    public const string AccessTier = PlatformClaimTypes.AccessTier;

    /// <summary>The member's role at issuance.</summary>
    public const string Role = PlatformClaimTypes.Role;

    /// <summary>
    /// One claim per permission. The snapshot authorisation checks read.
    /// <para>
    /// Aliased to the platform constant rather than re-declared: the kernel's permission
    /// handler reads this claim on every request, so the two names must be the same string
    /// by construction and not by coincidence.
    /// </para>
    /// </summary>
    public const string Permission = PlatformClaimTypes.Permission;

    /// <summary>One claim per open feature gate. Only present when configured.</summary>
    public const string Feature = "feat";

    /// <summary>
    /// The member's security stamp at issuance. Any change invalidates every token
    /// carrying an older value — the immediate kill switch that short expiry alone
    /// cannot provide.
    /// </summary>
    public const string SecurityStamp = "sst";

    /// <summary>Correlation id, so a token can be tied back to the request that issued it.</summary>
    public const string CorrelationId = "cid";

    /// <summary>
    /// Distinguishes a member's token from a trusted service's. Absent means member —
    /// the overwhelmingly common case, and the safer default: an unstamped token is
    /// validated against a live session and account, which a forged "service" claim
    /// could otherwise sidestep.
    /// </summary>
    public const string TokenType = "tty";

    /// <summary>Value of <see cref="TokenType"/> on a platform service token.</summary>
    public const string ServiceTokenType = "service";
}
