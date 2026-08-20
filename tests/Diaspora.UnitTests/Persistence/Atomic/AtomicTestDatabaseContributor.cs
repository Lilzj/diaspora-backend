using Diaspora.Persistence;
using Diaspora.Persistence.Modules;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.UnitTests.Persistence.Atomic;

/// <summary>
/// Maps the test resource into the real <see cref="DiasporaDbContext"/>, through the platform's own
/// contributor seam.
/// <para>
/// Column names are deliberately unlike the property names, and the schema is deliberately not the
/// default. If the primitive resolved either by convention rather than by asking the model, these
/// tests would fail — which is the point of choosing them.
/// </para>
/// </summary>
public sealed class AtomicTestDatabaseContributor : IDatabaseContributor
{
    public const string SchemaName = "atomic_test";

    public string Module => "AtomicTest";

    public string Schema => SchemaName;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<AtomicTestResource>(resource =>
        {
            resource.ToTable("resources", SchemaName);

            resource.HasKey(entity => entity.Id);

            resource.Property(entity => entity.Id)
                .HasColumnName("resource_id")
                .HasConversion(new StronglyTypedIdValueConverter<AtomicTestResourceId>());

            resource.Property(entity => entity.Name).HasColumnName("display_name").IsRequired();

            resource.Property(entity => entity.Status)
                .HasColumnName("lifecycle_state")
                .HasConversion<string>()
                .IsRequired();

            resource.Property(entity => entity.Revision)
                .HasColumnName("row_revision")
                .IsConcurrencyToken();

            resource.OwnsOne(entity => entity.Capacity, capacity =>
            {
                capacity.Property(value => value.Total).HasColumnName("units_total");
                capacity.Property(value => value.Held).HasColumnName("units_held");
                capacity.Property(value => value.Consumed).HasColumnName("units_consumed");
            });

            resource.Navigation(entity => entity.Capacity).IsRequired();

            resource.Ignore(entity => entity.DomainEvents);
        });
    }
}
