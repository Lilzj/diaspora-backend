using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Domain.Suggestions;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Persistence.Configurations;

/// <summary>
/// Maps improvement suggestions.
/// <para>
/// The (guide, status) index serves both screens the aggregate exists for: a guide's author
/// reviewing what is open against their guide, and the moderation queue sweeping every open
/// suggestion. One index, because both queries lead with the same columns.
/// </para>
/// </summary>
public sealed class GuideSuggestionConfiguration : IEntityTypeConfiguration<GuideSuggestion>
{
    public void Configure(EntityTypeBuilder<GuideSuggestion> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            GuidePersistenceConstants.GuideSuggestionsTable, GuidesModuleMetadata.Schema);

        builder.HasKey(suggestion => suggestion.Id);

        builder.Property(suggestion => suggestion.Id)
            .HasConversion(GuideValueConverters.Id<GuideSuggestionId>());

        builder.Property(suggestion => suggestion.GuideId)
            .HasConversion(GuideValueConverters.Id<GuideId>())
            .IsRequired();

        builder.Property(suggestion => suggestion.AuthorId)
            .HasConversion(GuideValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(suggestion => suggestion.DecidedBy)
            .HasConversion(GuideValueConverters.Id<UserId>());

        builder.Property(suggestion => suggestion.Status)
            .HasConversion<string>()
            .HasMaxLength(GuidePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(suggestion => suggestion.ProposedContent)
            .HasMaxLength(GuideLimits.MaxSuggestionContent)
            .IsRequired();

        builder.Property(suggestion => suggestion.Rationale)
            .HasMaxLength(GuideLimits.MaxSuggestionRationale);

        builder.Property(suggestion => suggestion.DecisionNote)
            .HasMaxLength(GuideLimits.MaxSuggestionRationale);

        builder.HasIndex(suggestion => new { suggestion.GuideId, suggestion.Status })
            .HasDatabaseName("ix_guide_suggestions_guide_status");

        builder.Ignore(suggestion => suggestion.DomainEvents);
    }
}
