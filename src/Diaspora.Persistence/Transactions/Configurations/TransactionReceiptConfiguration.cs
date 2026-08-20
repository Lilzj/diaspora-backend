using Diaspora.Platform.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Persistence.Transactions.Configurations;

/// <summary>
/// Maps the commit-verification receipt.
/// <para>
/// The narrowest table in the platform, and intentionally so: it is written once per transaction on
/// a retrying provider, read only when a commit acknowledgement is lost, and purged within the
/// hour. Every column it does not have is a column the write path does not pay for.
/// </para>
/// <para>
/// <b>No value converters, and that is load-bearing.</b> The row is inserted by a raw command that
/// bypasses the change tracker — <c>DiasporaDbContext</c> explains why it must — and a raw command
/// bypasses converters with it. A converted column would be written one way by ADO.NET and read
/// another by EF, and the verifying query would answer "no" for a commit that succeeded. A string
/// and a long mean both paths agree on every provider.
/// </para>
/// </summary>
public sealed class TransactionReceiptConfiguration : IEntityTypeConfiguration<TransactionReceipt>
{
    /// <summary>A "D"-format GUID. Fixed width, so the column can be too.</summary>
    public const int IdLength = 36;

    public void Configure(EntityTypeBuilder<TransactionReceipt> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(TransactionSchema.Receipts, TransactionSchema.Name);

        builder.HasKey(receipt => receipt.Id);

        builder.Property(receipt => receipt.Id)
            .HasMaxLength(IdLength)
            .IsRequired();

        builder.Property(receipt => receipt.CreatedAtTicks)
            .IsRequired();

        // Computed from the ticks for a human reading the table; there is no column behind it.
        builder.Ignore(receipt => receipt.CreatedAt);

        // The retention sweep's cutoff. Without it the sweep scans a table that exists to be
        // swept, which is the one table where that would be embarrassing.
        builder.HasIndex(receipt => receipt.CreatedAtTicks)
            .HasDatabaseName("ix_transaction_receipts_created_at");
    }
}
