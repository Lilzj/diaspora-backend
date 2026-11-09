using System.Net;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// What an unauthenticated caller actually receives, over HTTP, from the composed host.
/// <para>
/// <b>BSEC-003's verification criterion, and the only place it can be checked.</b> The
/// architecture suite can assert that <c>GetMyCreatorActivityQuery</c> carries
/// <c>[Authorize]</c>; it cannot assert that the request is refused, because the refusal is
/// composed from four things that only exist together in a booted host — the JWT handler,
/// <c>FallbackPolicy = null</c>, the route's own (absent) metadata, and
/// <c>AuthorizationBehavior</c> throwing inside the MediatR pipeline. Whether that produces a 401
/// or a 500 is not deducible from any one of them.
/// </para>
/// <para>
/// <b>Both directions, deliberately.</b> A rule that only asserted the 401 would pass just as well
/// if the endpoint had been broken for everybody, and "the fix refused the attacker" is worth very
/// little next to "and it still serves the member". The audit's own XC-007 — safety properties
/// asserted rather than executed — is the reason this is a request through the host and not a
/// comment.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class AnonymousAccessTests : PooledIdentityTestBase
{
    /// <summary>
    /// The route BSEC-003 confirmed reachable without a token. No route in
    /// <c>Diaspora.Modules.Creator/Api</c> declares <c>RequireAuthorization</c>, so the attribute
    /// on the query is the whole of its protection.
    /// </summary>
    private const string CreatorActivityRoute = "/api/v1/creators/me/activity";

    public AnonymousAccessTests(CustomWebApplicationFactory factory) : base(factory) =>
        factory.EnsureDatabaseCreated();

    [Fact]
    public async Task An_anonymous_caller_is_refused_the_creator_activity_timeline()
    {
        // A client of its own, rather than the base class's: that one carries whatever
        // Authorization header the suite last set on it, and a test for anonymity that silently
        // sent a token would pass for the wrong reason.
        using var anonymous = Factory.CreateClient();

        var response = await anonymous.GetAsync(CreatorActivityRoute);

        response.StatusCode.Should().Be(
            HttpStatusCode.Unauthorized,
            "this returned 200 before BSEC-003 was fixed — the handler's own RequireUser() kept "
            + "the data in, so the route answered an unauthenticated caller rather than refusing "
            + "one, which is defence in depth being asked to do authorisation's job");
    }

    [Fact]
    public async Task A_verified_member_still_reaches_their_own_creator_activity_timeline()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        // RegisterVerifiedMemberAsync signs in again to pick up the new access state and returns
        // those tokens, but clears the client's header on the way through and does not reapply
        // them. Without this line the request goes out anonymous and the test passes for the
        // reason it was written to rule out.
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync(CreatorActivityRoute);

        response.StatusCode.Should().NotBe(
            HttpStatusCode.Unauthorized,
            "creator:profile:read is granted from the Onboarding tier upward, so a verified member "
            + "holds it; a 401 here would mean the fix closed the route rather than guarding it");

        response.StatusCode.Should().NotBe(
            HttpStatusCode.Forbidden,
            "Participate would have produced this — it is what the module requires of an act, and "
            + "every sibling caller-scoped read takes ProfileRead instead");
    }
}
