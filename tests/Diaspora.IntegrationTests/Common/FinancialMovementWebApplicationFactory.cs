using Diaspora.Modules.Financial.Domain.ValueObjects;
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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Financial:EnableDeposits"] = "true",
                ["Financial:EnableRewardConversion"] = "true",

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

    public string Provider => "test-settlement";

    /// <summary>What <see cref="SubmitAsync"/> answers. False models a provider refusal.</summary>
    public bool Accepts { get; set; } = true;

    /// <summary>True to model a provider that throws rather than answering.</summary>
    public bool Throws { get; set; }

    public IReadOnlyList<SettlementInstruction> Submitted => _submitted;

    public Money? LastAmount => _submitted.Count == 0 ? null : _submitted[^1].Amount;

    public Task<bool> SubmitAsync(
        SettlementInstruction instruction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instruction);

        if (Throws)
        {
            throw new InvalidOperationException("The settlement provider is unreachable.");
        }

        _submitted.Add(instruction);

        return Task.FromResult(Accepts);
    }

    public void Reset()
    {
        _submitted.Clear();
        Accepts = true;
        Throws = false;
    }
}
