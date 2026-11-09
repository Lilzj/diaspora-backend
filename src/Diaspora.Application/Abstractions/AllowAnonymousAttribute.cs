namespace Diaspora.Application.Abstractions;

/// <summary>
/// States that a request deliberately requires no authenticated caller.
/// <para>
/// <b>The absence of <see cref="AuthorizeAttribute"/> is not a decision anybody can read.</b> It is
/// the same state whether the author meant "anyone may call this" or forgot the attribute entirely,
/// and BSEC-003 is what that cost: <c>GET /api/v1/creators/me/activity</c> served
/// <c>GetMyCreatorActivityQuery</c> to unauthenticated callers for as long as the route existed,
/// because <c>AuthorizationBehavior</c> returns <c>next()</c> when it finds no attribute and no
/// route in that module declares authorisation either. Nothing in the build, the suite or the
/// analyzer set objected, and the architecture test a comment in
/// <c>IdentityAuthenticationRegistration</c> claimed would catch it had never been written.
/// </para>
/// <para>
/// This attribute makes the two states distinguishable. A request type carrying it says the public
/// reach is intended — sign-in, registration, token refresh, password recovery, a
/// signature-verified payment webhook, published reference content — and
/// <see cref="RequestAuthorizationArchitectureTests"/> requires every request type to carry this or
/// <see cref="AuthorizeAttribute"/>.
/// </para>
/// <para>
/// <b>It grants nothing and disables nothing.</b> A route that declares
/// <c>RequireAuthorization</c> still refuses an anonymous caller before the pipeline runs; this
/// attribute is a statement about what the CQRS layer requires, not an override of the HTTP layer.
/// Handlers that scope to the caller must still ask — <c>RequireUser()</c> remains correct and
/// necessary.
/// </para>
/// <para>
/// <b>Why <c>AuthorizationBehavior</c> does not yet refuse an unmarked request.</b> It is meant to,
/// and that is the remaining half of BSEC-003. At the time this was introduced 247 request types
/// carried neither attribute — most of them correctly public reference content, some protected by
/// their route, some reached only from background services — and flipping the behaviour before each
/// one is classified would refuse live traffic rather than harden it. They are enumerated in
/// <c>RequestsAwaitingAuthorizationReview.txt</c>, which the architecture rules hold at its current
/// size or smaller. The behaviour changes when that list is empty.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AllowAnonymousAttribute : Attribute
{
    /// <summary>
    /// Why this request is reachable without authentication. Required, because the value of this
    /// attribute over an absent one is that somebody had to write down what they meant.
    /// </summary>
    public AllowAnonymousAttribute(string because)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(because);

        Because = because;
    }

    /// <summary>The stated reason this request needs no authenticated caller.</summary>
    public string Because { get; }
}
