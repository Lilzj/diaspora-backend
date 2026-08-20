using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Modules.Financial.Extensibility.Gateways;
using Diaspora.Modules.Financial.Observability;
using Diaspora.Modules.Financial.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Financial.Infrastructure.Hosting;

/// <summary>
/// Checks the platform's belief about a payment against the provider's own record.
/// <para>
/// <b>Where the two disagree, neither side is trusted blindly, and the rules are written down here
/// rather than emerging from whichever branch a maintainer wrote first.</b>
/// </para>
/// <list type="bullet">
/// <item>
/// <b>Provider settled, platform captured.</b> The platform advances to <c>Settled</c>. This is the
/// ordinary case and the only one that is not a problem: capture is what the platform believes,
/// settlement is what the provider confirms, and the gap between them is a normal delay.
/// </item>
/// <item>
/// <b>Provider settled, platform never captured.</b> <em>Nothing is applied.</em> The platform does
/// not manufacture a capture from a reconciliation file — a capture posts to the ledger and settles
/// a purchase, and doing that from a batch job days later would activate a placement somebody has
/// stopped expecting. It is logged and counted as a mismatch for a person to look at, because the
/// member has been charged and needs somebody to say so.
/// </item>
/// <item>
/// <b>Platform captured, provider has no record.</b> Also a mismatch, also nothing applied, and the
/// more serious direction: the platform believes it has money nobody sent.
/// </item>
/// </list>
/// <para>
/// <b>Does nothing when no reconciliation provider is registered.</b> None ships in this assembly.
/// The counter it exports still exists so a deployment can see that reconciliation is not running.
/// </para>
/// </summary>
public sealed class PaymentReconciliationSweep : FinancialSweep
{
    public PaymentReconciliationSweep(
        IServiceScopeFactory scopes,
        IOptions<FinancialOptions> options,
        ILogger<PaymentReconciliationSweep> logger)
        : base(scopes, options, logger)
    {
    }

    protected override bool IsEnabled => Options.EnableReconciliationSweep;

    protected override TimeSpan Interval => TimeSpan.FromMinutes(Options.ReconciliationSweepMinutes);

    public override async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = Scopes.CreateScope();

        var providers = scope.ServiceProvider
            .GetServices<IPaymentReconciliationProvider>()
            .ToList();

        if (providers.Count == 0)
        {
            return 0;
        }

        var intents = scope.ServiceProvider.GetRequiredService<IPaymentIntentRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var now = clock.UtcNow;
        var staleAfter = now.AddHours(-Math.Max(1, Options.ReconciliationAfterHours));
        var from = now.AddHours(-Math.Max(1, Options.ReconciliationAfterHours) * 2);
        var handled = 0;

        // The other direction, and the more serious one: the platform believes it has money and no
        // provider has confirmed it. Collected before the provider records are read so that a
        // payment which appears in neither is counted once and not missed.
        var unsettled = await intents.ListUnsettledAsync(
            staleAfter, Options.SweepBatchSize, cancellationToken);

        var confirmed = new HashSet<Guid>();

        foreach (var provider in providers)
        {
            var records = await FetchAsync(provider, from, now, cancellationToken);

            foreach (var record in records)
            {
                var intent = await intents.FindByExternalReferenceAsync(
                    record.Provider, record.ExternalReference, cancellationToken);

                if (intent is null)
                {
                    Mismatch(provider.Provider, "no_matching_intent");

                    continue;
                }

                confirmed.Add(intent.Id.Value);

                if (intent.Status == PaymentIntentStatus.Captured
                    && record.Status.Equals("settled", StringComparison.OrdinalIgnoreCase))
                {
                    intent.Settle(clock.UtcNow, record.Status);
                    intents.Update(intent);
                    handled++;

                    continue;
                }

                if (intent.Status is PaymentIntentStatus.Captured or PaymentIntentStatus.Settled)
                {
                    continue;
                }

                // The provider says it took the money and the platform never recorded a capture.
                // Nothing is applied — see the class summary — but somebody has to be told.
                Mismatch(provider.Provider, "provider_ahead");

                Logger.LogError(
                    "Reconciliation mismatch: {Provider} reports {Status} for payment "
                    + "{PaymentIntentId} which this platform holds as {Held}.",
                    provider.Provider,
                    record.Status,
                    intent.Id.Value,
                    intent.Status);
            }
        }

        // Captured here, unknown to every provider that answered. Nothing is applied — a capture the
        // platform cannot substantiate is not a capture to undo from a batch job, and reversing one
        // would take back a placement or a subscription somebody is using. It is counted and logged,
        // which is what "there must be a safe reconciliation mechanism" means when neither side can
        // be trusted alone.
        foreach (var intent in unsettled.Where(intent => !confirmed.Contains(intent.Id.Value)))
        {
            Mismatch(intent.GatewayProvider ?? "unknown", "platform_ahead");

            Logger.LogError(
                "Reconciliation mismatch: this platform holds payment {PaymentIntentId} as captured "
                + "since {CapturedAt} and no provider settlement record accounts for it.",
                intent.Id.Value,
                intent.CapturedAt);
        }

        if (handled > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return handled;
    }

    private static void Mismatch(string provider, string kind) =>
        FinancialTelemetry.ReconciliationMismatches.Add(
            1,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("kind", kind));

    private async Task<IReadOnlyList<ReconciliationRecord>> FetchAsync(
        IPaymentReconciliationProvider provider,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        try
        {
            return await provider.FetchAsync(from, to, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031
        catch (Exception exception)
#pragma warning restore CA1031
        {
            Logger.LogError(
                exception,
                "Could not fetch {Provider}'s settlement records. Reconciliation is not running for "
                + "it and the next pass will try again.",
                provider.Provider);

            return [];
        }
    }
}
