using Diaspora.Application;
using Diaspora.Application.Abstractions;
using Diaspora.Application.Behaviors;
using FluentAssertions;
using Diaspora.Platform.Modules;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The step-up gate's position and reach (BSEC-007).
/// <para>
/// <b>These rules exist because the finding was not a bug.</b> Nothing was broken: a withdrawal
/// needed one permission, got it, and worked. The defect was that no rule said it should need
/// anything more — so the gate's value lasts exactly as long as somebody remembers it is there.
/// These are the memory.
/// </para>
/// <para>
/// <b>The pipeline order is read from the registration, not from the source file.</b> A regex over
/// <c>DependencyInjection.cs</c> would pass on a comment and fail on a reformat; building the
/// service collection asks the thing that actually decides. This repository has already paid for
/// text-shaped rules twice — a prefix check that worked by accident, and eight rules reading a
/// gitignored tree.
/// </para>
/// </summary>
public class StepUpArchitectureTests
{
    /// <summary>
    /// <b>After authorisation, before the transaction.</b>
    /// <para>
    /// Both halves are load-bearing. Before authorisation, a caller who will never hold
    /// <c>payments:manage</c> would be told to go and fetch a code — which is both useless to them
    /// and a disclosure of which operations the platform guards this way. After the transaction, a
    /// refused request would have opened one for nothing, and on a retrying execution strategy that
    /// is a refusal that costs a connection each time.
    /// </para>
    /// </summary>
    [Fact]
    public void The_step_up_gate_runs_after_authorization_and_before_the_transaction()
    {
        var behaviors = new ServiceCollection()
            .AddApplication()
            .Where(descriptor => descriptor.ServiceType == typeof(IPipelineBehavior<,>))
            .Select(descriptor => descriptor.ImplementationType)
            .ToList();

        var authorization = behaviors.IndexOf(typeof(AuthorizationBehavior<,>));
        var stepUp = behaviors.IndexOf(typeof(StepUpBehavior<,>));
        var transaction = behaviors.IndexOf(typeof(TransactionBehavior<,>));

        stepUp.Should().BeGreaterThan(-1, "the gate has to be registered to gate anything");

        stepUp.Should().BeGreaterThan(
            authorization,
            "a caller without the permission must be told that, not sent to fetch a code they "
            + "cannot use — and must not learn which operations are step-up-guarded");

        stepUp.Should().BeLessThan(
            transaction,
            "a refused request must never have opened a transaction");
    }

    /// <summary>
    /// <b>The guarded set, by name.</b>
    /// <para>
    /// Four commands, and the list is here rather than derived, deliberately. There is no property
    /// of a command that says "this moves money" — <c>[Transactional]</c> is about atomicity and
    /// <c>[Auditable]</c> is about the trail, and a rule keyed on either would sweep in half the
    /// platform. So the decision is written down, and changing it means changing this list, which
    /// means somebody says why in a diff.
    /// </para>
    /// <para>
    /// <b><c>VerifyPayoutDestinationCommand</c> is absent on purpose.</b> BSEC-007's remediation
    /// names it; its own evidence names <c>RefreshPayoutDestinationCommand</c>, which is the
    /// member's route to the same outcome. Verify is staff recording that somebody who could check
    /// did, and a step-up there would text a code to the staff member — a different control for a
    /// different threat, and one that would break staff tooling to no purpose.
    /// </para>
    /// </summary>
    [Fact]
    public void Exactly_these_commands_require_a_fresh_proof_of_identity()
    {
        var expected = new[]
        {
            "Diaspora.Modules.Financial.Application.Withdrawals.CreateWithdrawalCommand",
            "Diaspora.Modules.Financial.Application.Withdrawals.RegisterPayoutDestinationCommand",
            "Diaspora.Modules.Financial.Application.Withdrawals.RegisterPayoutIdentityCommand",
            "Diaspora.Modules.Financial.Application.Withdrawals.RefreshPayoutDestinationCommand"
        };

        Guarded().Select(type => type.FullName!)
            .Should().BeEquivalentTo(
                expected,
                "adding or removing a step-up gate is a decision about somebody's money, and it "
                + "belongs in a diff rather than in a reviewer's memory");
    }

    /// <summary>
    /// <b>Every guarded command also names its authorisation.</b>
    /// <para>
    /// The gate runs after the authorisation behaviour, so a guarded command carrying no
    /// <c>[Authorize]</c> would reach the gate with nobody having been identified — and the gate
    /// would answer an anonymous caller with "fetch a code", which both discloses the guard and
    /// asks for something an anonymous caller has no way to provide. Step-up is a second factor; it
    /// is not a first one.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_step_up_guarded_command_names_its_authorization()
    {
        var offenders = Guarded()
            .Where(type => type.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false).Length == 0)
            .Select(type => type.FullName!)
            .ToList();

        offenders.Should().BeEmpty(
            "step-up is a second factor behind an identified caller, never a first one");
    }

    /// <summary>
    /// Every scope a command names is one the platform declares.
    /// </summary>
    /// <remarks>
    /// A misspelled scope is neither a compile error nor a startup error. The gate simply never
    /// finds a grant for it, so the command refuses every caller forever and the member is told to
    /// confirm something nothing can confirm. The failure mode is a permanently broken withdrawal
    /// that looks like a security control working.
    /// </remarks>
    [Fact]
    public void Every_scope_a_command_names_is_one_the_platform_declares()
    {
        var offenders = Guarded()
            .Select(type => (type, scope: Scope(type)))
            .Where(entry => !StepUpScopes.All.Contains(entry.scope))
            .Select(entry => $"{entry.type.FullName} names '{entry.scope}'")
            .ToList();

        offenders.Should().BeEmpty(
            "a scope StepUpScopes does not define can never be granted, and nothing says so");
    }

    /// <summary>
    /// Every command on the platform carrying the attribute, found by scanning rather than listed.
    /// </summary>
    /// <remarks>
    /// <see cref="ModuleAssemblyScanner"/> is the platform's own discovery, the same one the module
    /// registry uses, so a module that appears tomorrow is in scope without this file changing. The
    /// application assembly is added because the attribute lives there and a future Identity
    /// command may well use it.
    /// </remarks>
    private static List<Type> Guarded() =>
        ModuleAssemblyScanner.FindPlatformAssemblies()
            .Concat([typeof(ICommand<>).Assembly])
            .Distinct()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type
                .GetCustomAttributes(typeof(RequiresStepUpAttribute), inherit: false).Length > 0)
            .ToList();

    private static string Scope(Type type) =>
        ((RequiresStepUpAttribute)type
            .GetCustomAttributes(typeof(RequiresStepUpAttribute), inherit: false)[0]).Scope;
}
