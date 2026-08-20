using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace Diaspora.Modules.Notifications.Infrastructure.Realtime;

/// <summary>
/// Tells SignalR which claim identifies a member.
/// <para>
/// <b>Registered because the default is wrong for this platform, and wrong in a way nothing would
/// report.</b> SignalR's default provider reads <see cref="ClaimTypes.NameIdentifier"/>. Identity
/// issues its subject as <c>sub</c> and sets <c>MapInboundClaims = false</c>, which means the usual
/// SOAP-era claim URIs are never synthesised — so the default provider resolves null, every
/// <c>Clients.User(...)</c> send reaches nobody, and there is no error anywhere. A member simply
/// stops receiving anything.
/// </para>
/// <para>
/// <b>The fallback chain exists because a test host is not a deployment.</b> The integration suite
/// authenticates with the real token pipeline, but a future host — a different scheme, a delegated
/// token — may spell the subject differently. Trying <c>sub</c>, then the mapped name-identifier,
/// then <c>nameid</c> costs three dictionary lookups on connect and removes a whole class of "works
/// locally, silent in staging".
/// </para>
/// <para>
/// The value is returned verbatim rather than parsed into a <c>UserId</c>: SignalR keys its own user
/// index on this string, and normalising it here while the group names are composed from a Guid
/// elsewhere would give one member two identities.
/// </para>
/// </summary>
public sealed class RealtimeUserIdProvider : IUserIdProvider
{
    /// <summary>
    /// The registered claim Identity issues its subject as.
    /// <para>
    /// A literal rather than <c>JwtRegisteredClaimNames.Sub</c>, so that reading a claim name does
    /// not cost this module a dependency on a JWT library. The value is fixed by RFC 7519 and cannot
    /// change; the constant it would otherwise be read from is a spelling of the same three letters.
    /// </para>
    /// </summary>
    public const string SubjectClaim = "sub";

    /// <summary>The short form some token pipelines emit instead.</summary>
    public const string LegacySubjectClaim = "nameid";

    public string? GetUserId(HubConnectionContext connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var user = connection.User;

        if (user is null)
        {
            return null;
        }

        return user.FindFirst(SubjectClaim)?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst(LegacySubjectClaim)?.Value;
    }
}
