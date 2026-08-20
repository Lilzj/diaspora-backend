using Diaspora.Application.Identity.Models;

namespace Diaspora.Application.Identity.Abstractions;

/// <summary>
/// Mints tokens for trusted platform services rather than for members.
/// <para>
/// The Trust &amp; Verification Engine has to call Identity to deliver a verdict, and it is
/// not a person: it has no password, no phone number and no session. Forcing it through the
/// member authentication path would mean creating a fake account with a real credential —
/// a far worse outcome than giving service callers their own, narrower token type.
/// </para>
/// <para>
/// Service tokens carry an explicit token-type claim, hold only the permissions they are
/// issued for, and never carry a session. Token validation branches on that claim, so a
/// service token can never be mistaken for a member's and vice versa.
/// </para>
/// </summary>
public interface IServiceTokenIssuer
{
    AccessTokenDescriptor Issue(ServiceTokenRequest request);
}
