using Diaspora.Persistence;
using Diaspora.Persistence.Modules;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.UnitTests.Persistence;

/// <summary>
/// Maps the test aggregate into the real <see cref="DiasporaDbContext"/>.
/// <para>
/// Through the platform's own <see cref="IDatabaseContributor"/> seam rather than a bespoke test
/// context, so these tests exercise the actual context every module saves through — the same
/// audit pipeline, the same soft-delete conversion, the same query filters. A test double of the
/// context would be a test of the double.
/// </para>
/// </summary>
public sealed class SoftDeleteTestDatabaseContributor : IDatabaseContributor
{
    public const string SchemaName = "softdelete_test";

    public string Module => "SoftDeleteTest";

    public string Schema => SchemaName;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<SoftDeleteTestOrder>(order =>
        {
            order.ToTable("orders", SchemaName);

            order.HasKey(entity => entity.Id);

            order.Property(entity => entity.Id)
                .HasConversion(new StronglyTypedIdValueConverter<SoftDeleteTestOrderId>());

            order.Property(entity => entity.Reference).IsRequired();

            // The token EF appends to the WHERE clause of every update, including the update a
            // soft delete becomes.
            order.Property(entity => entity.Version).IsConcurrencyToken();

            // Owned reference on the shared table. Both columns are required, which is what
            // turns the pre-B1.0.1 cascade into a constraint violation rather than silent
            // corruption.
            order.OwnsOne(entity => entity.ShippingAddress, address =>
            {
                address.Property(value => value.City).HasColumnName("city").IsRequired();
                address.Property(value => value.Postcode).HasColumnName("postcode").IsRequired();
            });

            // Owned reference containing a nested owned value object.
            order.OwnsOne(entity => entity.Total, total =>
            {
                total.Property(value => value.Amount).HasColumnName("total_amount").IsRequired();

                total.OwnsOne(value => value.Currency, currency =>
                    currency.Property(inner => inner.Code)
                        .HasColumnName("total_currency")
                        .IsRequired());

                total.Navigation(value => value.Currency).IsRequired();
            });

            order.Navigation(entity => entity.ShippingAddress).IsRequired();
            order.Navigation(entity => entity.Total).IsRequired();

            // Owned collection in its own table.
            order.OwnsMany(entity => entity.Lines, line =>
            {
                line.ToTable("order_lines", SchemaName);

                line.HasKey(value => value.Id);

                line.Property(value => value.Id)
                    .HasConversion(new StronglyTypedIdValueConverter<SoftDeleteTestLineId>());

                line.Property(value => value.Sku).IsRequired();

                line.WithOwner().HasForeignKey("order_id");

                line.Ignore(value => value.DomainEvents);
            });

            order.Ignore(entity => entity.DomainEvents);
        });
    }
}
