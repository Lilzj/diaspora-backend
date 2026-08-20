using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Modules.Financial.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Financial.Infrastructure.Hosting;

/// <summary>
/// Closes payment intents nobody finished.
/// <para>
/// <b>An abandoned intent is not harmless.</b> It holds an idempotency key, appears in the member's
/// payment history as though something is still happening, and would let a client resume a payment
/// against a price that may have changed. Expiring it is the honest outcome.
/// </para>
/// </summary>
public sealed class PaymentExpirySweep : FinancialSweep
{
    public PaymentExpirySweep(
        IServiceScopeFactory scopes,
        IOptions<FinancialOptions> options,
        ILogger<PaymentExpirySweep> logger)
        : base(scopes, options, logger)
    {
    }

    protected override bool IsEnabled => Options.EnablePaymentExpirySweep;

    protected override TimeSpan Interval => TimeSpan.FromMinutes(Options.PaymentExpirySweepMinutes);

    public override async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = Scopes.CreateScope();

        var intents = scope.ServiceProvider.GetRequiredService<IPaymentIntentRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var now = clock.UtcNow;
        var handled = 0;

        foreach (var intent in await intents.ListExpiredAsync(
            now, Options.SweepBatchSize, cancellationToken))
        {
            intent.Cancel(now, expired: true);
            intents.Update(intent);
            handled++;
        }

        if (handled > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return handled;
    }
}
