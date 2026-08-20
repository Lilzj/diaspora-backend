using System.Reflection;
using Diaspora.Modules.Financial;
using FluentAssertions;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// An idempotency key is half a key. The other half is whose it is.
/// <para>
/// <b>The defect this prevents.</b> Every member-facing money route in the Financial module
/// accepts an <c>idempotencyKey</c> in its body and replays it by looking the key up. Those lookups
/// matched on the key <em>alone</em>, with no owner predicate, which made the key a global name
/// rather than a per-member one. A member whose key collided with another member's was answered
/// with <em>that member's</em> record — a payment intent's identifier, amount, currency and capture
/// state; a withdrawal's amount and destination; a conversion's points and credited amount — at
/// HTTP 200. Their own operation was never created. Neither party saw an error.
/// </para>
/// <para>
/// <b>Why a collision needs no malice.</b> A client that derives its key from an operation name and
/// a subject id — which is the natural way to write one, and what this platform's own mobile client
/// does — produces keys that collide between members by construction. The mobile client's key
/// generator is documented as a collision-avoidance token rather than a cryptographic one, which is
/// the correct judgement for a member-scoped namespace and the wrong one for a global namespace.
/// The client was right about its own half; the server's half was missing.
/// </para>
/// <para>
/// <b>Why this is a signature test rather than a data test.</b> A data test proves one repository
/// scopes one query on the day it is written. This proves that <em>no</em> lookup in the module can
/// be called without an owner at all — including one added next year by somebody who never read
/// this file. The fix was to make the owner a required leading parameter; this asserts it stays
/// required, because an optional owner is an owner a call site can omit, and omitting it is exactly
/// what the defect was.
/// </para>
/// <para>
/// One call site — <c>CreateDonationCommandHandler</c> — already compared
/// <c>intent.PayerUserId</c> against the caller after the lookup and refused a mismatch. Four did
/// not. That is the argument for putting the check where it cannot be forgotten rather than
/// where it must be remembered.
/// </para>
/// </summary>
public class IdempotencyScopingArchitectureTests
{
    private static readonly Assembly FinancialModule = typeof(FinancialModuleMetadata).Assembly;

    /// <summary>
    /// Every idempotency lookup in the Financial module takes an owner as its first parameter.
    /// </summary>
    [Fact]
    public void EveryIdempotencyLookup_TakesAnOwnerFirst()
    {
        var lookups = IdempotencyLookups(FinancialModule);

        lookups.Should().NotBeEmpty(
            "the Financial module replays idempotency keys; if this finds nothing the method has "
            + "been renamed and this guard has stopped guarding anything.");

        using var _ = new FluentAssertions.Execution.AssertionScope();

        foreach (var lookup in lookups)
        {
            var parameters = lookup.GetParameters();
            var owner = parameters.ElementAtOrDefault(0);

            owner.Should().NotBeNull(
                $"{Describe(lookup)} must take an owner identifier.");

            owner!.ParameterType.Should().Be<Guid>(
                $"{Describe(lookup)} must scope its lookup to a member. Matching on the key alone "
                + "returns another member's record to whoever guessed or collided with their key.");

            owner.HasDefaultValue.Should().BeFalse(
                $"{Describe(lookup)} must not let a caller omit the owner. An optional owner is an "
                + "owner somebody forgets, and forgetting it is the defect.");
        }
    }

    /// <summary>
    /// No overload exists that takes only the key — the shape the defect had.
    /// </summary>
    [Fact]
    public void NoIdempotencyLookup_AcceptsTheKeyAlone()
    {
        var unscoped = IdempotencyLookups(FinancialModule)
            .Where(lookup =>
            {
                var parameters = lookup.GetParameters();
                var required = parameters.Where(p => !p.HasDefaultValue).ToArray();

                // A lookup whose only required argument is the key is unscoped by construction,
                // whatever else it accepts.
                return required.Length == 1 && required[0].ParameterType == typeof(string);
            })
            .Select(Describe)
            .ToArray();

        unscoped.Should().BeEmpty(
            "an idempotency lookup keyed on the string alone is a cross-member read. "
            + "Offending members: " + string.Join(", ", unscoped));
    }

    // `List<>` rather than `IReadOnlyList<>` because CA1859 is an error in Release
    // (`TreatWarningsAsErrors`), and this file shipped emitting it. The call sites
    // only enumerate and count, so the concrete type costs nothing in coupling.
    private static List<MethodInfo> IdempotencyLookups(Assembly assembly) =>
        assembly
            .GetTypes()
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method => method.Name.StartsWith("FindByIdempotencyKey", StringComparison.Ordinal))
            .ToList();

    private static string Describe(MethodInfo method) =>
        $"{method.DeclaringType?.Name}.{method.Name}";
}
