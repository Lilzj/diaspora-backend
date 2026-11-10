using Diaspora.Application.Abstractions;
using Diaspora.Application.Behaviors;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Application;

/// <summary>
/// The step-up gate (BSEC-007), pinned at the three decisions that make it a control rather than a
/// decoration.
/// <para>
/// <b>It costs nothing on an unguarded request</b>, which is every request but a handful. <b>It
/// refuses a guarded one with its own exception type</b>, so a client can tell "collect a code and
/// retry" from "you will never be allowed this". And <b>it fails closed when the service is
/// missing</b>, which is the opposite of its neighbour <c>PostingStandingBehavior</c> and the whole
/// reason the two are written differently.
/// </para>
/// </summary>
public class StepUpBehaviorTests
{
    private readonly IStepUpService _stepUp = Substitute.For<IStepUpService>();

    /// <summary>
    /// An ordinary request passes without the service being resolved at all.
    /// </summary>
    /// <remarks>
    /// The assertion that nothing was asked is the point. This behaviour runs on every request on
    /// the platform, so an unguarded one must not pay for a scoped resolution, a database read, or
    /// anything else.
    /// </remarks>
    [Fact]
    public async Task An_unguarded_request_is_not_checked_at_all()
    {
        var behavior = Behavior<PlainCommand>();
        var handlerRan = false;

        await behavior.Handle(
            new PlainCommand(),
            () => { handlerRan = true; return Task.FromResult(Result.Success()); },
            CancellationToken.None);

        handlerRan.Should().BeTrue();

        await _stepUp.DidNotReceive().HasProvenAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_guarded_request_proceeds_when_the_session_has_proved_itself()
    {
        _stepUp.HasProvenAsync(StepUpScopes.Withdrawal, Arg.Any<CancellationToken>()).Returns(true);

        var behavior = Behavior<GuardedCommand>();
        var handlerRan = false;

        await behavior.Handle(
            new GuardedCommand(),
            () => { handlerRan = true; return Task.FromResult(Result.Success()); },
            CancellationToken.None);

        handlerRan.Should().BeTrue();
    }

    /// <summary>
    /// <b>The handler is never reached.</b> Asserted as well as the exception, because a gate that
    /// threw after the work had been done would be worse than no gate: the money would have moved
    /// and the caller would be told it had not.
    /// </summary>
    [Fact]
    public async Task A_guarded_request_is_refused_when_it_has_not()
    {
        _stepUp.HasProvenAsync(StepUpScopes.Withdrawal, Arg.Any<CancellationToken>()).Returns(false);

        var behavior = Behavior<GuardedCommand>();
        var handlerRan = false;

        var refusal = await Assert.ThrowsAsync<Diaspora.Application.Exceptions.StepUpRequiredException>(
            () => behavior.Handle(
                new GuardedCommand(),
                () => { handlerRan = true; return Task.FromResult(Result.Success()); },
                CancellationToken.None));

        handlerRan.Should().BeFalse("the gate runs before the handler, not after it");

        refusal.Scope.Should().Be(
            StepUpScopes.Withdrawal,
            "the refusal carries what to ask for, or the client has to guess");
    }

    /// <summary>
    /// <b>No step-up service, no withdrawal.</b>
    /// <para>
    /// <c>PostingStandingBehavior</c> resolves its port optionally and lets the request through when
    /// nothing is registered, which is right for it: a host composed without Trust blocks nobody.
    /// The opposite is right here, so this behaviour resolves with <c>GetRequiredService</c> and a
    /// misconfigured host fails loudly on the first guarded request instead of quietly serving every
    /// one of them.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_guarded_request_fails_rather_than_passing_when_the_service_is_absent()
    {
        var empty = new ServiceCollection().BuildServiceProvider();
        var behavior = new StepUpBehavior<GuardedCommand, Result>(empty);
        var handlerRan = false;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => behavior.Handle(
                new GuardedCommand(),
                () => { handlerRan = true; return Task.FromResult(Result.Success()); },
                CancellationToken.None));

        handlerRan.Should().BeFalse("failing closed means the handler does not run");
    }

    /// <summary>
    /// Every scope an attribute can name is one the platform declares, checked over the whole
    /// application assembly rather than over a list kept by hand.
    /// </summary>
    /// <remarks>
    /// A misspelled scope is not a compile error and not a startup error: the gate would simply
    /// never find a grant for it, so the guarded command would refuse every caller forever and the
    /// member would be told to confirm something no code can confirm. Cheap to catch here.
    /// </remarks>
    [Fact]
    public void Every_declared_step_up_scope_is_one_the_platform_defines()
    {
        var offenders = typeof(ICommand<>).Assembly
            .GetTypes()
            .Concat(typeof(Diaspora.Modules.Financial.FinancialModule).Assembly.GetTypes())
            .Select(type => (type, attribute: type
                .GetCustomAttributes(typeof(RequiresStepUpAttribute), inherit: false)
                .Cast<RequiresStepUpAttribute>()
                .FirstOrDefault()))
            .Where(entry => entry.attribute is not null)
            .Where(entry => !StepUpScopes.All.Contains(entry.attribute!.Scope))
            .Select(entry => $"{entry.type.FullName} names '{entry.attribute!.Scope}'")
            .ToList();

        offenders.Should().BeEmpty(
            "a scope the gate does not know refuses every caller forever, and nothing says so");
    }

    private StepUpBehavior<TRequest, Result> Behavior<TRequest>()
        where TRequest : notnull
    {
        var services = new ServiceCollection();
        services.AddSingleton(_stepUp);

        return new StepUpBehavior<TRequest, Result>(services.BuildServiceProvider());
    }

    private sealed record PlainCommand;

    [RequiresStepUp(StepUpScopes.Withdrawal)]
    private sealed record GuardedCommand;
}
