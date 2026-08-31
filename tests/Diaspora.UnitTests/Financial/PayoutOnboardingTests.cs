using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Extensibility.Payouts;
using Diaspora.Modules.Financial.Extensibility.Settlement;
using Diaspora.Modules.Financial.Infrastructure.Payouts;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Financial;

/// <summary>
/// The second question a payout has to answer: not "can this provider serve this corridor" but
/// "has this person completed that provider's own onboarding".
/// <para>
/// <b>Two verification records exist on this platform and they are not the same thing.</b> The
/// platform's is <c>FinancialAccount.Status</c>, event-sourced from Trust, and it is what
/// <c>RequiresVerifiedPayee</c> answers. A provider's is its own connected-account state, and a
/// member Trust has fully verified may have done nothing at all with a provider they have never
/// been routed to. Conflating them would let one satisfy the other.
/// </para>
/// <para>
/// Providers here are invented, for the reason <see cref="PayoutRoutingTests"/> gives.
/// </para>
/// </summary>
public class PayoutOnboardingTests
{
    private const string Alpha = "alpha";
    private const string Beta = "beta";

    private static readonly Guid Payee = new("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task A_route_that_needs_no_onboarding_is_taken_without_asking_anybody()
    {
        var source = new StubVerification(Alpha, PayoutProviderKycStatus.NotStarted);

        var resolver = Resolver([Row(Alpha)], source);

        var result = await resolver.ResolveAsync(Request(), Payee);

        result.IsRouted.Should().BeTrue();
        source.Asked.Should().BeFalse(
            "a provider that does not require onboarding must not be interrogated about it");
    }

    [Fact]
    public async Task A_verified_payee_is_carried_by_a_route_that_requires_onboarding()
    {
        var resolver = Resolver(
            [Row(Alpha, requiresOnboarding: true)],
            new StubVerification(Alpha, PayoutProviderKycStatus.Verified));

        var result = await resolver.ResolveAsync(Request(), Payee);

        result.IsRouted.Should().BeTrue();
        result.Provider.Should().Be(Alpha);
    }

    /// <summary>
    /// Every status except <c>Verified</c> means do not pay. <c>Pending</c> especially: a provider
    /// reviewing somebody has not agreed to pay them, and treating "nearly" as "yes" is how a payout
    /// is attempted against an account that cannot receive it.
    /// </summary>
    [Theory]
    [InlineData(PayoutProviderKycStatus.NotStarted)]
    [InlineData(PayoutProviderKycStatus.Pending)]
    [InlineData(PayoutProviderKycStatus.Rejected)]
    [InlineData(PayoutProviderKycStatus.Restricted)]
    [InlineData(PayoutProviderKycStatus.Expired)]
    public async Task Any_status_short_of_verified_refuses(PayoutProviderKycStatus status)
    {
        var resolver = Resolver(
            [Row(Alpha, requiresOnboarding: true)], new StubVerification(Alpha, status));

        var result = await resolver.ResolveAsync(Request(), Payee);

        result.IsRouted.Should().BeFalse();
        result.Refusal.Should().Be(PayoutRefusalReason.ProviderOnboardingRequired);
    }

    /// <summary>
    /// <b>A requirement nothing can report on refuses rather than passes.</b> No verification source
    /// ships, so a deployment that marks a row <c>RequiresProviderOnboarding</c> and registers
    /// nothing to answer it gets a refusal. The alternative — treating "we could not ask" as "they
    /// are fine" — would make the flag decorative at exactly the moment it mattered.
    /// </summary>
    [Fact]
    public async Task A_requirement_with_no_source_behind_it_fails_closed()
    {
        var resolver = Resolver([Row(Alpha, requiresOnboarding: true)]);

        var result = await resolver.ResolveAsync(Request(), Payee);

        result.IsRouted.Should().BeFalse();
        result.Refusal.Should().Be(PayoutRefusalReason.ProviderOnboardingRequired);
        result.Refusal.ToErrorCode().Should()
            .Be(Diaspora.Modules.Financial.FinancialErrorCodes.PayoutProviderOnboardingRequired);
    }

    /// <summary>
    /// <b>The one place an alternate route may legitimately be taken.</b> Nothing has been submitted,
    /// so choosing a different provider costs nothing and risks nothing. After submission this is
    /// never true again — a provider that timed out may already have moved the money.
    /// </summary>
    [Fact]
    public async Task An_alternate_carries_it_when_the_preferred_provider_needs_onboarding_nobody_did()
    {
        var resolver = Resolver(
            [
                Row(Alpha, requiresOnboarding: true) with { Priority = 1 },
                Row(Beta) with { Priority = 2 }
            ],
            new StubVerification(Alpha, PayoutProviderKycStatus.Pending));

        var result = await resolver.ResolveAsync(Request(), Payee);

        result.IsRouted.Should().BeTrue();
        result.Provider.Should().Be(Beta, "the preferred provider cannot carry this payee");
    }

    [Fact]
    public async Task The_preferred_provider_still_wins_when_its_onboarding_is_done()
    {
        var resolver = Resolver(
            [
                Row(Alpha, requiresOnboarding: true) with { Priority = 1 },
                Row(Beta) with { Priority = 2 }
            ],
            new StubVerification(Alpha, PayoutProviderKycStatus.Verified));

        var result = await resolver.ResolveAsync(Request(), Payee);

        result.Provider.Should().Be(Alpha);
    }

    /// <summary>
    /// A source that throws is treated as "not onboarded", not allowed to escape. An exception here
    /// would take down a sweep that is processing other people's money.
    /// </summary>
    [Fact]
    public async Task A_source_that_throws_is_treated_as_not_onboarded()
    {
        var resolver = Resolver(
            [Row(Alpha, requiresOnboarding: true)], new ThrowingVerification(Alpha));

        var result = await resolver.ResolveAsync(Request(), Payee);

        result.IsRouted.Should().BeFalse();
        result.Refusal.Should().Be(PayoutRefusalReason.ProviderOnboardingRequired);
    }

    /// <summary>
    /// The refusal carries what the provider is waiting for, so a support agent can tell somebody
    /// which document is missing rather than sending them to the logs.
    /// </summary>
    [Fact]
    public async Task The_refusal_names_what_the_provider_is_still_waiting_for()
    {
        var resolver = Resolver(
            [Row(Alpha, requiresOnboarding: true)],
            new StubVerification(Alpha, PayoutProviderKycStatus.Pending)
            {
                Requirements = ["identity_document", "proof_of_address"]
            });

        var result = await resolver.ResolveAsync(Request(), Payee);

        result.Detail.Should().Contain("identity_document").And.Contain("proof_of_address");
    }

    /// <summary>
    /// An alternate that requires onboarding nobody checked is left out rather than listed
    /// optimistically — it is a route a caller could take and be refused by.
    /// </summary>
    [Fact]
    public async Task Unchecked_onboarding_routes_are_not_offered_as_alternates()
    {
        var resolver = Resolver(
            [
                Row(Alpha) with { Priority = 1 },
                Row(Beta, requiresOnboarding: true) with { Priority = 2 }
            ],
            new StubVerification(Beta, PayoutProviderKycStatus.Verified));

        var result = await resolver.ResolveAsync(Request(), Payee);

        result.Provider.Should().Be(Alpha);
        result.Alternates.Should().BeEmpty(
            "the second route was never checked, so offering it would be a guess");
    }

    [Fact]
    public async Task An_unroutable_corridor_never_reaches_the_onboarding_question()
    {
        var source = new StubVerification(Alpha, PayoutProviderKycStatus.Verified);

        var resolver = Resolver([Row(Alpha, requiresOnboarding: true)], source);

        var result = await resolver.ResolveAsync(
            Request() with { Country = "MU" }, Payee);

        result.Refusal.Should().Be(PayoutRefusalReason.CountryUnsupported);
        source.Asked.Should().BeFalse("there is nothing to be onboarded with");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────

    private static PayoutCapability Row(string provider, bool requiresOnboarding = false) =>
        new()
        {
            Provider = provider,
            Country = "GB",
            Method = PayoutDestinationKind.BankAccount,
            Currency = "GBP",
            Support = CapabilitySupport.Supported,
            RequiresProviderOnboarding = requiresOnboarding
        };

    private static PayoutRouteRequest Request() =>
        new()
        {
            Country = "GB",
            Method = PayoutDestinationKind.BankAccount,
            Amount = Money.FromMinorUnits(10_000, "GBP"),
            PayeeVerified = true
        };

    private static PayoutRouteResolver Resolver(
        IReadOnlyList<PayoutCapability> rows,
        params IPayoutProviderVerificationSource[] sources) =>
        new(
            new PayoutProviderRouter(
                [new StubSource(rows)], [new StubSettlement(Alpha), new StubSettlement(Beta)]),
            sources);

    private sealed class StubSource(IReadOnlyList<PayoutCapability> rows) : IPayoutCapabilitySource
    {
        public string Name => "stub";

        public IReadOnlyList<PayoutCapability> GetCapabilities() => rows;
    }

    private sealed class StubSettlement(string provider) : ISettlementProvider
    {
        public string Provider => provider;

        public Task<SettlementOutcome> SubmitAsync(
            SettlementInstruction instruction, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Resolving must never submit.");
    }

    private sealed class StubVerification(string provider, PayoutProviderKycStatus status)
        : IPayoutProviderVerificationSource
    {
        public string Provider => provider;

        public IReadOnlyList<string> Requirements { get; init; } = [];

        /// <summary>Whether anybody asked. Pins that the fast path really is a fast path.</summary>
        public bool Asked { get; private set; }

        public Task<PayoutProviderVerification> GetAsync(
            Guid payeeUserId, CancellationToken cancellationToken = default)
        {
            Asked = true;

            return Task.FromResult(new PayoutProviderVerification
            {
                Provider = provider,
                Status = status,
                OutstandingRequirements = Requirements
            });
        }
    }

    private sealed class ThrowingVerification(string provider) : IPayoutProviderVerificationSource
    {
        public string Provider => provider;

        public Task<PayoutProviderVerification> GetAsync(
            Guid payeeUserId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The provider's API is down.");
    }
}
