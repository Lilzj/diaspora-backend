using Diaspora.Persistence.Transactions.Configurations;
using Diaspora.Platform.Usage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Persistence.Usage.Configurations;

/// <summary>
/// Maps the vendor allowance counter into the <c>platform</c> schema.
/// <para>
/// No value converters, for the same reason as the transaction receipt: the only writer is a raw
/// upsert, and a converted column would be stored one way and read another.
/// </para>
/// </summary>
public sealed class ProviderUsageCounterConfiguration : IEntityTypeConfiguration<ProviderUsageCounter>
{
    public const string Table = "provider_usage";

    public void Configure(EntityTypeBuilder<ProviderUsageCounter> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, TransactionSchema.Name);

        builder.HasKey(counter => counter.Id);

        builder.Property(counter => counter.Id).HasMaxLength(160).IsRequired();
        builder.Property(counter => counter.Provider).HasMaxLength(40).IsRequired();
        builder.Property(counter => counter.Sku).HasMaxLength(80).IsRequired();
        builder.Property(counter => counter.Period).HasMaxLength(7).IsRequired();
        builder.Property(counter => counter.Used).IsRequired();
    }
}
