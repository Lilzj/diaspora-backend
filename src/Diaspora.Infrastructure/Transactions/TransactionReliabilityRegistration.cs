using Diaspora.Platform.Transactions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.Infrastructure.Transactions;

/// <summary>
/// Registers the transaction subsystem's hosting.
/// <para>
/// There is very little to register, and that is the point. Transactional execution is owned
/// entirely by the unit of work — no service to resolve, no strategy to configure, no delegate for
/// a caller to supply. What is left is the retention sweep for the verification receipts and the
/// options that pace it.
/// </para>
/// <para>
/// The storage port is registered by the persistence layer, beside the <c>DbContext</c> it reads,
/// for the same reason the outbox's is.
/// </para>
/// </summary>
public static class TransactionReliabilityRegistration
{
    public static IServiceCollection AddTransactionReliability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<TransactionReliabilityOptions>()
            .Bind(configuration.GetSection(TransactionReliabilityOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHostedService<TransactionReceiptRetentionHostedService>();

        return services;
    }
}
