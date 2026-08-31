using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Extensibility.Payouts;
using Diaspora.Modules.Financial.Extensibility.Settlement;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.IntegrationTests.Common;

/// <summary>
/// The host B15.1's money-movement suite runs against.
/// <para>
/// <b>A separate host because the capabilities it turns on are exactly what the default host asserts
/// are off.</b> Deposits and reward conversion are capability switches a deployment opts into, and
/// <c>The_capabilities_endpoint_reports_conversion_and_payouts_as_unavailable</c> asserts the shipped
/// default. Turning them on globally would make that test prove the opposite of what it was written
/// for.
/// </para>
/// <para>
/// <b>The settlement provider is registered here and could not be registered anywhere else.</b> No
/// settlement engine ships in the Financial assembly — an architecture test asserts the absence — so
/// the only way to exercise a payout end to end is a deployment-side registration, which is exactly
/// what a real deployment does. This is that, in a test assembly.
/// </para>
/// <para>
/// The contributor set is identical to the default host's, so the two share one EF model cache
/// safely — the platform's "one model cache per DbContext type" trap bites only when the mapped
/// types differ.
/// </para>
/// </summary>
public class FinancialMovementWebApplicationFactory : CustomWebApplicationFactory
{
    /// <summary>What the fake provider does with an instruction. Set per test.</summary>
    public FakeSettlementProvider Settlement { get; } = new();

    /// <summary>The second adapter, serving a different corridor. See the sweep routing test.</summary>
    public FakeSettlementProvider AlternateSettlement { get; } =
        new(FakeSettlementProvider.AlternateProviderName);

    /// <summary>What the platform is told when it asks about an unresolved payout.</summary>
    public FakeSettlementStatusProvider Status { get; } = new();

    /// <summary>The provider's own record of what it paid. Drives the reconciliation sweep.</summary>
    public FakePayoutReconciliationProvider Reconciliation { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Financial:EnableDeposits"] = "true",
                ["Financial:EnableRewardConversion"] = "true",

                // The payout capability matrix, which a deployment must configure before any
                // withdrawal can route. One row, matching the destination the suite registers: the
                // fake provider, a British bank account, in sterling. Without it every withdrawal
                // refuses with financial.payout.country_unsupported, which is the router doing its
                // job rather than a broken harness — a provider being registered says nothing about
                // where it can send money.
                ["Financial:PayoutRouting:Capabilities:0:Provider"] = FakeSettlementProvider.ProviderName,
                ["Financial:PayoutRouting:Capabilities:0:Country"] = "GB",
                ["Financial:PayoutRouting:Capabilities:0:Currency"] = "GBP",
                ["Financial:PayoutRouting:Capabilities:0:Method"] = nameof(PayoutDestinationKind.BankAccount),
                ["Financial:PayoutRouting:Capabilities:0:Support"] = nameof(CapabilitySupport.Supported),

                // A second corridor that only the alternate adapter serves. Kenyan mobile money is
                // a rail the first adapter's row says nothing about, so a sweep that ignored the
                // route would hand it to the wrong provider.
                ["Financial:PayoutRouting:Capabilities:1:Provider"] = FakeSettlementProvider.AlternateProviderName,
                ["Financial:PayoutRouting:Capabilities:1:Country"] = "KE",
                ["Financial:PayoutRouting:Capabilities:1:Currency"] = "GBP",
                ["Financial:PayoutRouting:Capabilities:1:Method"] = nameof(PayoutDestinationKind.MobileMoney),
                ["Financial:PayoutRouting:Capabilities:1:Support"] = nameof(CapabilitySupport.Supported),

                // Every B15.1 sweep is off for the reason every other background loop in this suite
                // is: one shared in-memory SQLite connection, and a loop querying it mid-command is a
                // concurrent use SQLite will not allow. Tests drive RunOnceAsync directly.
                ["Financial:EnableWithdrawalSweep"] = "false",
                ["Financial:EnableExpiryNoticeSweep"] = "false",
                ["Financial:EnableReconciliationSweep"] = "false",
                ["Financial:EnablePaymentExpirySweep"] = "false",
                ["Financial:EnablePlacementSweep"] = "false",
                ["Financial:EnableSubscriptionSweep"] = "false",
                ["Financial:EnableCreditExpirySweep"] = "false",
                ["Financial:EnableRevenueRollupSweep"] = "false",
                ["Financial:EnableGivingReconciliationSweep"] = "false",
                ["Financial:EnableCampaignSweep"] = "false",
                ["Financial:EnableRecurringDonationSweep"] = "false",
                ["Financial:EnableMembershipBindingReconciliation"] = "false"
            }));

        builder.ConfigureServices(services =>
        {
            // Additive: the module scans for ISettlementProvider and finds none in its own assembly,
            // so this is the deployment's registration arriving after the module's.
            services.AddSingleton(Settlement);
            services.AddSingleton<ISettlementProvider>(Settlement);

            // The second rail. Registered after the first, so a sweep that took whichever adapter
            // came back first would take the wrong one for the corridor it serves.
            services.AddSingleton<ISettlementProvider>(AlternateSettlement);

            // Somewhere to ask what became of an instruction whose outcome is unknown. Off by
            // default (Answers is null), so the suite proves both halves: a held withdrawal stays
            // held when nothing can answer, and resolves when something can.
            services.AddSingleton(Status);
            services.AddSingleton<ISettlementStatusProvider>(Status);

            // The provider's own record of what it paid. Empty by default, so the sweep is inert
            // unless a test hands it something to find.
            services.AddSingleton(Reconciliation);
            services.AddSingleton<IPayoutReconciliationProvider>(Reconciliation);
        });
    }
}

/// <summary>
/// A settlement provider that does whatever the test tells it to.
/// <para>
/// Deliberately not called a simulator and deliberately not in the Financial assembly: the
/// architecture test that keeps settlement unimplemented there is one of the phase's central claims,
/// and a "simulated settlement provider" shipped for convenience would quietly repeal it.
/// </para>
/// </summary>
public sealed class FakeSettlementProvider : ISettlementProvider
{
    private readonly List<SettlementInstruction> _submitted = [];

    /// <summary>
    /// The name the capability matrix has to use. A constant rather than a literal in two places:
    /// the matrix routes on this string, and a matrix naming an adapter that does not answer to it
    /// refuses every payout with financial.payout.provider_not_registered.
    /// </summary>
    public const string ProviderName = "test-settlement";

    /// <summary>
    /// A second adapter, so the suite can prove the sweep routes per withdrawal. With one provider
    /// registered, "hands it to the right one" and "hands it to the only one" are the same
    /// assertion, and the FirstOrDefault this replaced would have passed it.
    /// </summary>
    public const string AlternateProviderName = "test-settlement-alt";

    public FakeSettlementProvider(string? provider = null) => Provider = provider ?? ProviderName;

    public string Provider { get; }

    /// <summary>What <see cref="SubmitAsync"/> answers. False models a provider refusal.</summary>
    public bool Accepts { get; set; } = true;

    /// <summary>
    /// True to model a provider whose outcome is unknown — a timeout, a 5xx. Distinct from
    /// <see cref="Accepts"/> being false, which is a provider positively declining, and the two
    /// must do different things to the member's money.
    /// </summary>
    public bool Unresolved { get; set; }

    /// <summary>True to model a provider that throws rather than answering.</summary>
    public bool Throws { get; set; }

    public IReadOnlyList<SettlementInstruction> Submitted => _submitted;

    public Money? LastAmount => _submitted.Count == 0 ? null : _submitted[^1].Amount;

    public Task<SettlementOutcome> SubmitAsync(
        SettlementInstruction instruction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instruction);

        if (Throws)
        {
            throw new InvalidOperationException("The settlement provider is unreachable.");
        }

        _submitted.Add(instruction);

        if (Unresolved)
        {
            return Task.FromResult(
                SettlementOutcome.Unresolved("test.unknown", "The provider did not say."));
        }

        return Task.FromResult(Accepts
            ? SettlementOutcome.Accepted("test-transfer")
            : SettlementOutcome.Refused("test.refused", "The provider refused it."));
    }

    public void Reset()
    {
        _submitted.Clear();
        Accepts = true;
        Throws = false;
        Unresolved = false;
    }
}

/// <summary>
/// Answers what became of an instruction, when a test says it should.
/// <para>
/// <see cref="Answers"/> is null by default and the provider reports <c>Unresolved</c>, which models
/// the deployment that has registered nowhere to ask: a held withdrawal stays held. A test sets it to
/// drive the resolution path.
/// </para>
/// </summary>
public sealed class FakeSettlementStatusProvider : ISettlementStatusProvider
{
    public string Provider => FakeSettlementProvider.ProviderName;

    /// <summary>Null means "cannot say". Set to an outcome to resolve the next question.</summary>
    public SettlementOutcome? Answers { get; set; }

    public int Asked { get; private set; }

    public Task<SettlementOutcome> GetOutcomeAsync(
        string reference, CancellationToken cancellationToken = default)
    {
        Asked++;

        return Task.FromResult(Answers
            ?? SettlementOutcome.Unresolved("test.cannot_say", "Nothing to go on."));
    }

    public void Reset()
    {
        Answers = null;
        Asked = 0;
    }
}

/// <summary>
/// Stands in for a provider's own record of the payouts it made.
/// <para>
/// Empty by default so the reconciliation sweep does nothing; a test adds rows to drive it. The
/// interesting row is one whose reference matches no withdrawal — money the provider says it sent
/// with nothing here accounting for it, which is the finding the sweep exists for.
/// </para>
/// </summary>
public sealed class FakePayoutReconciliationProvider : IPayoutReconciliationProvider
{
    private readonly List<PayoutReconciliationRecord> _records = [];

    public string Provider => FakeSettlementProvider.ProviderName;

    /// <summary>True to model a provider whose reconciliation endpoint is down.</summary>
    public bool Throws { get; set; }

    public void Add(PayoutReconciliationRecord record) => _records.Add(record);

    public Task<IReadOnlyList<PayoutReconciliationRecord>> FetchAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        if (Throws)
        {
            throw new InvalidOperationException("The reconciliation endpoint is unreachable.");
        }

        return Task.FromResult<IReadOnlyList<PayoutReconciliationRecord>>([.. _records]);
    }

    public void Reset()
    {
        _records.Clear();
        Throws = false;
    }
}
