using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Location.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="AdministrativeArea"/> aggregate.
/// <para>
/// The indexes here are the module's performance contract, and each one exists for a query
/// the product actually makes:
/// </para>
/// <list type="bullet">
/// <item><b>path</b> — the hierarchy. Every "inside X" question is a prefix match on this
/// column, and it is the reason the tree needs no recursive query.</item>
/// <item><b>(latitude, longitude)</b> — every spatial query starts as four inequalities on
/// this pair. It is what lets radius, viewport and nearest work at speed with no spatial
/// extension installed.</item>
/// <item><b>(level, country)</b> — "every city in the UK", which is the cities and regions
/// endpoints and the country page.</item>
/// <item><b>(slug, level)</b> — the neighbourhood hub route the frontend already uses.</item>
/// <item><b>(parent, prominence)</b> — hierarchy browsing, in display order.</item>
/// </list>
/// </summary>
public sealed class AdministrativeAreaConfiguration : IEntityTypeConfiguration<AdministrativeArea>
{
    public void Configure(EntityTypeBuilder<AdministrativeArea> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(LocationPersistenceConstants.AreasTable, LocationModuleMetadata.Schema);

        builder.HasKey(area => area.Id);

        builder.Property(area => area.Id)
            .HasConversion(LocationValueConverters.Id<AreaId>());

        builder.Property(area => area.Level)
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();

        builder.OwnsOne(area => area.Name, name =>
        {
            name.Property(value => value.Value)
                .HasColumnName("name")
                .HasMaxLength(LocationPersistenceConstants.NameLength)
                .IsRequired();

            name.Property(value => value.LocalName)
                .HasColumnName("local_name")
                .HasMaxLength(LocationPersistenceConstants.NameLength);

            // Searched on every keystroke of the location selector, in both spellings.
            name.HasIndex(value => value.Value).HasDatabaseName("ix_areas_name");
        });

        builder.Navigation(area => area.Name).IsRequired();

        builder.Property(area => area.Slug)
            .HasConversion(LocationValueConverters.Slug)
            .HasMaxLength(LocationPersistenceConstants.SlugLength)
            .IsRequired();

        builder.Property(area => area.CountryId)
            .HasConversion(LocationValueConverters.Id<CountryId>())
            .IsRequired();

        builder.Property(area => area.CountryCode)
            .HasConversion(LocationValueConverters.CountryCode)
            .HasMaxLength(LocationPersistenceConstants.CountryCodeLength)
            .IsRequired();

        builder.Property(area => area.ParentId)
            .HasConversion(LocationValueConverters.Id<AreaId>());

        builder.Property(area => area.PathValue)
            .HasColumnName("path")
            .HasMaxLength(LocationPersistenceConstants.PathLength)
            .IsRequired();

        ConfigureGeography(builder);

        builder.Property(area => area.PostalCode)
            .HasConversion(LocationValueConverters.PostalCode)
            .HasMaxLength(LocationPersistenceConstants.PostalCodeLength);

        builder.Property(area => area.Overview)
            .HasMaxLength(LocationPersistenceConstants.OverviewLength);

        builder.Property(area => area.SafetyScore);
        builder.Property(area => area.WalkabilityScore);

        builder.Property(area => area.CostOfLiving)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(area => area.Prominence).IsRequired();

        builder.Property(area => area.IsDeleted).IsRequired();
        builder.Property(area => area.CreatedAt).IsRequired();

        ConfigureIndexes(builder);

        builder.Ignore(area => area.DomainEvents);
        builder.Ignore(area => area.Depth);

        // Projected from PathValue rather than stored twice.
        builder.Ignore(area => area.Path);
    }

    private static void ConfigureGeography(EntityTypeBuilder<AdministrativeArea> builder)
    {
        builder.OwnsOne(area => area.Center, center =>
        {
            center.Property(value => value.Latitude).HasColumnName("latitude");
            center.Property(value => value.Longitude).HasColumnName("longitude");

            // The composite that every spatial query in the module leans on. Latitude first
            // because a bounding box is always more selective on latitude: a degree of
            // longitude shrinks toward the poles, a degree of latitude never does.
            center.HasIndex(value => new { value.Latitude, value.Longitude })
                .HasDatabaseName("ix_areas_coordinates");
        });

        builder.OwnsOne(area => area.Extent, extent =>
        {
            extent.Property(value => value.North).HasColumnName("extent_north");
            extent.Property(value => value.South).HasColumnName("extent_south");
            extent.Property(value => value.East).HasColumnName("extent_east");
            extent.Property(value => value.West).HasColumnName("extent_west");
        });

        builder.Property(area => area.Boundary)
            .HasConversion(LocationValueConverters.Boundary)
            .HasColumnName("boundary")
            .HasMaxLength(LocationPersistenceConstants.BoundaryLength);

        // Beside the ring it describes. Every column is nullable because most nodes have no
        // outline; the aggregate sets these exactly when it sets the ring.
        builder.OwnsOne(area => area.BoundaryProvenance, provenance =>
        {
            provenance.Property(value => value.Source)
                .HasColumnName("boundary_source")
                .HasMaxLength(BoundaryProvenance.SourceMaxLength);

            provenance.Property(value => value.Licence)
                .HasColumnName("boundary_licence")
                .HasMaxLength(BoundaryProvenance.LicenceMaxLength);

            provenance.Property(value => value.Attribution)
                .HasColumnName("boundary_attribution")
                .HasMaxLength(BoundaryProvenance.AttributionMaxLength);

            provenance.Property(value => value.SourceReference)
                .HasColumnName("boundary_source_reference")
                .HasMaxLength(BoundaryProvenance.SourceReferenceMaxLength);

            provenance.Property(value => value.RetrievedOn)
                .HasColumnName("boundary_retrieved_on");
        });
    }

    private static void ConfigureIndexes(EntityTypeBuilder<AdministrativeArea> builder)
    {
        builder.HasIndex(area => area.PathValue)
            .HasDatabaseName("ix_areas_path");

        builder.HasIndex(area => new { area.CountryCode, area.Level })
            .HasDatabaseName("ix_areas_country_level");

        builder.HasIndex(area => new { area.Slug, area.Level })
            .HasDatabaseName("ix_areas_slug_level");

        builder.HasIndex(area => new { area.ParentId, area.Prominence })
            .HasDatabaseName("ix_areas_parent_prominence");

        // Uniqueness is per parent, not global: there is a Springfield in twelve states and
        // all twelve are legitimate. Enforcing it globally would reject real geography; not
        // enforcing it at all would let one import run twice.
        builder.HasIndex(area => new { area.ParentId, area.Slug })
            .IsUnique()
            .HasDatabaseName("ix_areas_parent_slug");
    }
}
