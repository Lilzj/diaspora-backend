using Diaspora.Modules.Guides.Domain.CountryContext;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Guides.Persistence.Configurations;

/// <summary>
/// Maps the cross-vertical counter projection.
/// <para>
/// <b>Counters are rows, not columns, and that is what keeps the projection open-ended.</b> A
/// vertical that lands after B8.1 publishes a contract this module subscribes to and its counter
/// appears — no migration here, no column added to a table in a different module's release. Columns
/// would have been marginally faster to read and would have made every future vertical a schema
/// change in Guides.
/// </para>
/// <para>
/// No concurrency token. This is maintained from at-least-once delivery on a background thread, and
/// a conflict would dead-letter a message over a number that is already approximate by design; the
/// counters clamp at zero and can be rebuilt from an authoritative count instead.
/// </para>
/// </summary>
public sealed class CountryContextSummaryConfiguration
    : IEntityTypeConfiguration<CountryContextSummary>
{
    public void Configure(EntityTypeBuilder<CountryContextSummary> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            KnowledgePersistenceConstants.CountryContextSummariesTable,
            GuidesModuleMetadata.Schema);

        builder.HasKey(summary => summary.Id);

        builder.Property(summary => summary.Id)
            .HasConversion(GuideValueConverters.Id<CountryContextSummaryId>());

        builder.Property(summary => summary.CountryCode)
            .HasMaxLength(KnowledgePersistenceConstants.CountryCodeLength)
            .IsRequired();

        builder.HasIndex(summary => summary.CountryCode)
            .IsUnique()
            .HasDatabaseName("ux_knowledge_country_context_country");

        ConfigureCounters(builder);

        builder.Ignore(summary => summary.DomainEvents);
    }

    private static void ConfigureCounters(EntityTypeBuilder<CountryContextSummary> builder) =>
        builder.OwnsMany(summary => summary.Counters, counter =>
        {
            counter.ToTable(
                KnowledgePersistenceConstants.CountryContextCountersTable,
                GuidesModuleMetadata.Schema);

            counter.Property(value => value.Kind)
                .HasMaxLength(KnowledgePersistenceConstants.CounterKindLength)
                .IsRequired();

            counter.WithOwner().HasForeignKey("CountryContextSummaryId");

            counter.HasKey("CountryContextSummaryId", nameof(CountryContextCounter.Kind));
        });
}
