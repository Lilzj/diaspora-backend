namespace Diaspora.Contracts.Authorization;

/// <summary>
/// Claim names the platform itself reads.
/// <para>
/// Kept in Contracts rather than in the Identity module because the kernel's authorisation
/// handlers read them on every request. A module that issues tokens must use these names;
/// everything else about the token's shape is the issuer's business.
/// </para>
/// </summary>
public static class PlatformClaimTypes
{
    /// <summary>One claim per permission held. Short by design — it rides on every request.</summary>
    public const string Permission = "perm";

    /// <summary>The subject's access tier at issuance, as an <see cref="AccessTier"/> name.</summary>
    public const string AccessTier = "tier";

    /// <summary>The subject's role at issuance.</summary>
    public const string Role = "rol";

    /// <summary>
    /// The device the session was opened on, when the client named one — B13.3.
    /// <para>
    /// <b>Promoted here from the Identity module's own list for one reason: a module outside Identity
    /// needs to answer "is this the device I am talking to".</b> The Communication Platform records a
    /// push endpoint per device and the client used to decide which row was the current one from an
    /// identifier it generated itself — a guess the server could not check and a member could not be
    /// protected by. Reading it from the presented token makes it an assertion instead, and the
    /// alternative was Communication referencing Identity to learn the spelling of a string.
    /// </para>
    /// <para>
    /// Absent on a token issued to a caller that named no device, which is every service token and
    /// every sign-in from a client that did not send one. A consumer must treat absence as "cannot
    /// tell" rather than as "not this device".
    /// </para>
    /// </summary>
    public const string DeviceId = "did";
}
