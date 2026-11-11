using Diaspora.Application.Abstractions;
using Diaspora.Application.Behaviors;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Application;

/// <summary>
/// <c>AuthorizationBehavior</c> after BSEC-003: a request that declares nothing is refused.
/// <para>
/// <b>The first test is the finding.</b> The behaviour used to return <c>next()</c> when it found
/// no <c>[Authorize]</c>, so a request type nobody had classified reached its handler with no
/// authentication check of any kind — and because the host sets <c>FallbackPolicy = null</c>, a
/// route that also declared nothing served it to anonymous callers. One did
/// (<c>GET /api/v1/creators/me/activity</c>).
/// </para>
/// <para>
/// BSOLID-001 is the same defect stated as a shape: the absence of an attribute was a <em>valid</em>
/// state meaning "no check", so no reader, compiler or analyzer could tell a decision from an
/// omission. These tests pin both halves of the fix — the refusal, and the marker that makes the
/// public case say so.
/// </para>
/// </summary>
public class AuthorizationBehaviorTests
{
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IPermissionService _permissions = Substitute.For<IPermissionService>();

    /// <summary>
    /// <b>The defect, inverted.</b> Nothing about this request says who may send it, and the
    /// handler is never reached.
    /// </summary>
    [Fact]
    public async Task A_request_that_declares_nothing_is_refused()
    {
        var handlerRan = false;

        var act = async () => await Behavior<UnclassifiedCommand>().Handle(
            new UnclassifiedCommand(),
            () => { handlerRan = true; return Task.FromResult(Result.Success()); },
            CancellationToken.None);

        await act.Should().ThrowAsync<Diaspora.Application.Exceptions.UnauthorizedException>();

        handlerRan.Should().BeFalse(
            "before BSEC-003 this handler ran, for any caller, with no check of any kind");
    }

    /// <summary>
    /// <b>And it is refused even when the caller is authenticated</b>, which is the part that
    /// makes this a classification rule rather than an authentication one. An unclassified request
    /// is one nobody decided about, and a signed-in caller does not make the decision for them.
    /// </summary>
    [Fact]
    public async Task An_authenticated_caller_does_not_rescue_an_unclassified_request()
    {
        _currentUser.IsAuthenticated.Returns(true);

        var act = async () => await Behavior<UnclassifiedCommand>().Handle(
            new UnclassifiedCommand(),
            () => Task.FromResult(Result.Success()),
            CancellationToken.None);

        await act.Should().ThrowAsync<Diaspora.Application.Exceptions.UnauthorizedException>();
    }

    [Fact]
    public async Task A_request_marked_anonymous_proceeds_without_a_caller()
    {
        _currentUser.IsAuthenticated.Returns(false);

        var handlerRan = false;

        await Behavior<PublicQuery>().Handle(
            new PublicQuery(),
            () => { handlerRan = true; return Task.FromResult(Result.Success()); },
            CancellationToken.None);

        handlerRan.Should().BeTrue(
            "published reference content is read by people who have no account yet");
    }

    [Fact]
    public async Task An_authorized_request_still_refuses_an_anonymous_caller()
    {
        _currentUser.IsAuthenticated.Returns(false);

        var act = async () => await Behavior<MemberCommand>().Handle(
            new MemberCommand(),
            () => Task.FromResult(Result.Success()),
            CancellationToken.None);

        await act.Should().ThrowAsync<Diaspora.Application.Exceptions.UnauthorizedException>();
    }

    [Fact]
    public async Task A_policy_the_caller_does_not_satisfy_is_forbidden_rather_than_unauthorized()
    {
        _currentUser.IsAuthenticated.Returns(true);
        _permissions.AuthorizeAsync("testing.widgets.read", Arg.Any<CancellationToken>())
            .Returns(false);

        var act = async () => await Behavior<PolicyQuery>().Handle(
            new PolicyQuery(),
            () => Task.FromResult(Result.Success()),
            CancellationToken.None);

        await act.Should().ThrowAsync<Diaspora.Application.Exceptions.ForbiddenException>(
            "the caller is who they say they are and may not do this — a different answer from "
            + "not knowing who they are");
    }

    /// <summary>
    /// The policy arm asks <see cref="IPermissionService.AuthorizeAsync"/> and not
    /// <c>HasPermissionAsync</c>, which is what makes a dotted policy name resolvable at all.
    /// </summary>
    [Fact]
    public async Task A_satisfied_policy_proceeds_and_is_asked_as_a_policy()
    {
        _currentUser.IsAuthenticated.Returns(true);
        _permissions.AuthorizeAsync("testing.widgets.read", Arg.Any<CancellationToken>())
            .Returns(true);

        var handlerRan = false;

        await Behavior<PolicyQuery>().Handle(
            new PolicyQuery(),
            () => { handlerRan = true; return Task.FromResult(Result.Success()); },
            CancellationToken.None);

        handlerRan.Should().BeTrue();

        await _permissions.DidNotReceive().HasPermissionAsync(
            "testing.widgets.read", Arg.Any<CancellationToken>());
    }

    private AuthorizationBehavior<TRequest, Result> Behavior<TRequest>()
        where TRequest : notnull =>
        new(_currentUser, _permissions,
            NullLogger<AuthorizationBehavior<TRequest, Result>>.Instance);

    /// <summary>
    /// A request type nobody classified: the state BSEC-003 made unreachable at runtime.
    /// </summary>
    /// <remarks>
    /// <b>A real <c>ICommand</c>, and DSP4006 is suppressed for exactly one declaration.</b> The
    /// analyzer forbids this shape, which is the point of the analyzer — and this test needs the
    /// shape in order to prove the runtime refuses it too. Suppressing rather than sidestepping
    /// (by declaring a record that implements nothing) keeps the fixture faithful to the case: the
    /// defect was an unannotated <em>request</em>, not an unannotated POCO.
    /// <para>
    /// The two mechanisms are belt and brace. If this suppression is ever deleted and the fixture
    /// annotated to make the build quiet, this test stops testing anything and the three
    /// assertions below pass for free.
    /// </para>
    /// </remarks>
#pragma warning disable DSP4006
    private sealed record UnclassifiedCommand : ICommand;
#pragma warning restore DSP4006

    [AllowAnonymous("A test double standing in for published reference content.")]
    private sealed record PublicQuery;

    [RequireAuthentication("A test double for the fourteen requests that ask only for a caller.")]
    private sealed record MemberCommand;

    [RequirePolicy("testing.widgets.read")]
    private sealed record PolicyQuery;
}
