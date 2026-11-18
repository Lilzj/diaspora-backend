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
/// <para>
/// <b>Retired as unreachable, 18 November 2026 (BSEC-013, BSOLID-007).</b> Everything above is
/// still true of the token <em>shape</em>, and the platform still validates one — the branch in
/// <c>AccessTokenValidationHandler</c> is live and the integration suite exercises it. What is not
/// true is that this platform mints one. The Trust &amp; Verification Engine runs outside this
/// process and signs its own token with the shared key, so no production code has ever called
/// this; its only callers in the repository are two lines of test infrastructure.
/// </para>
/// <para>
/// <b>Why that was worth acting on rather than leaving.</b> A registered, unreachable
/// <em>security</em> capability reads as an implemented control during a review — the audit says
/// this happened to it and to <c>OtpPurpose.StepUpAuthentication</c> in the same pass. And the
/// principal it produces skips every live-state check by design: no session, no account lookup, no
/// security stamp. A component that can mint one has no reason to be resolvable from the container
/// until something needs it.
/// </para>
/// <para>
/// <b>What would revive it.</b> A decision to run the Trust Engine, or any other service caller,
/// <em>inside</em> this process. Reviving it should come with the second half of BSEC-013 that is
/// not fixed here: service tokens are signed with the member signing key, so a separate
/// <c>kid</c>-scoped key is what stops the member key minting one. That is key material, and it
/// belongs with BSEC-008.
/// </para>
/// </summary>
/// <seealso href="../../../../docs/adrs/adr-020-retiring-an-abstraction.md"/>
[Obsolete(
    "IServiceTokenIssuer is retired as unreachable: no production code mints a service token, " +
    "because the Trust Engine signs its own outside this process. The platform still validates " +
    "service tokens and that path is unchanged. Do not resolve or implement this — if a service " +
    "caller is ever hosted in-process, revive it together with a kid-scoped signing key so the " +
    "member key cannot mint one. See ADR-020, and BSEC-013 and BSOLID-007 of the November 2026 " +
    "pre-launch audit.",
    error: true,
    DiagnosticId = "DSP7001")]
public interface IServiceTokenIssuer
{
    AccessTokenDescriptor Issue(ServiceTokenRequest request);
}
