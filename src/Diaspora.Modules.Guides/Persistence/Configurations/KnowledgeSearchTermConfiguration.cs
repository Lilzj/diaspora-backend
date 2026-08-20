using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.KnowledgeAnalytics;
using Diaspora.Modules.Guides.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Guides.Persistence.Configurations;

/// <summary>
/// Maps the per-country search phrase counters.
/// <para>
/// <b>The country column is the empty string rather than null for a search that named none.</b> The
/// unique index spans (country, term), and a nullable column in a unique index treats every null as
/// distinct on several providers — which would turn one global phrase into a fresh row on every
/// single search, and the table into a log. The domain enforces the same convention so the two
/// cannot disagree.
/// </para>
/// <para>
/// No concurrency token, for the same reason the usage counters carry none: a lost increment is one
/// search among thousands, and refusing a search because two people typed the same phrase at once
/// would be an absurd trade.
/// </para>
/// </summary>
public sealed class KnowledgeSearchTermConfiguration : IEntityTypeConfiguration<KnowledgeSearchTerm>
{
    public void Configure(EntityTypeBuilder<KnowledgeSearchTerm> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            KnowledgePersistenceConstants.SearchTermsTable, GuidesModuleMetadata.Schema);

        builder.HasKey(term => term.Id);

        builder.Property(term => term.Id)
            .HasConversion(GuideValueConverters.Id<KnowledgeSearchTermId>());

        builder.Property(term => term.CountryCode)
            .HasMaxLength(KnowledgePersistenceConstants.CountryCodeLength)
            .IsRequired();

        builder.Property(term => term.Term)
            .HasMaxLength(KnowledgeLimits.MaxSearchTerm)
            .IsRequired();

        builder.HasIndex(term => new { term.CountryCode, term.Term })
            .IsUnique()
            .HasDatabaseName("ux_knowledge_search_terms_country_term");

        // The trending list: most searched within a country, recently.
        builder.HasIndex(term => new { term.CountryCode, term.SearchCount })
            .HasDatabaseName("ix_knowledge_search_terms_country_count");

        // The commissioning queue: phrases that currently find nothing.
        builder.HasIndex(term => new { term.CountryCode, term.ResultCount })
            .HasDatabaseName("ix_knowledge_search_terms_country_results");

        builder.Ignore(term => term.DomainEvents);
        builder.Ignore(term => term.IsUnanswered);
    }
}
