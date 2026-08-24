using Diaspora.Modules.Housing.Persistence.Configurations;
using Diaspora.Persistence.Modules;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Housing.Persistence;

/// <summary>
/// Adds the Housing module's tables to the shared context.
/// <para>
/// Twenty-one tables in the module's own <c>housing</c> schema, which the context verifies at
/// startup. Eleven configurations, not twenty-one, because ten of those tables are owned by an
/// aggregate and are configured from inside it — the aggregate boundary made visible in the
/// persistence layer: if a table can be reached without loading its root, it should not be owned.
/// </para>
/// <para>
/// <b>Six of the eleven aggregates are <c>ISoftDeletable</c>, so the shared context applies the
/// global query filter and <c>SoftDeleteConverter</c> handles the owned rows on delete.</b> Nothing
/// here declares either, deliberately — B1.0.1 exists so a module never has to.
/// </para>
/// <para>
/// The five that are not are reference data, member-owned rows and the measured record. An
/// accommodation type and an amenity are retired with a flag rather than deleted, because listings
/// store their keys. An endorsement is hard-deleted, because withdrawing one is a member asking the
/// platform to forget an opinion. A report is a record of a decision and is never removed. And
/// analytics outlives the listing's visibility, because a neighbourhood's average rent is computed
/// from properties that were let years ago.
/// </para>
/// <para>
/// <b><c>housing_neighborhoods</c> holds neighbourhood profiles, not neighbourhoods.</b> The places
/// themselves are B4's <c>location_areas</c> and each profile row points at one. A reader who takes
/// the table name at face value will draw exactly the wrong conclusion about what this module owns.
/// </para>
/// </summary>
public sealed class HousingDatabaseContributor : IDatabaseContributor
{
    public string Module => HousingModuleMetadata.Name;

    public string Schema => HousingModuleMetadata.Schema;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // The listing aggregate — owns its amenities, house rules, media, provider and guide
        // references and its moderation trail.
        modelBuilder.ApplyConfiguration(new HousingListingConfiguration());

        // Who is letting. Owns only value objects, so one table.
        modelBuilder.ApplyConfiguration(new LandlordProfileConfiguration());

        // The relocation workflow. Separate aggregates keyed by listing — a popular property
        // attracts dozens of viewings, and loading them with the listing would make a card render
        // read somebody's whole diary.
        modelBuilder.ApplyConfiguration(new HousingViewingConfiguration());
        modelBuilder.ApplyConfiguration(new HousingReservationConfiguration());
        modelBuilder.ApplyConfiguration(new LandlordReviewConfiguration());

        // The neighbourhood profile and what the community says about it. The insight is its own
        // aggregate because the moderation queue pages across neighbourhoods rather than within
        // one.
        modelBuilder.ApplyConfiguration(new NeighborhoodProfileConfiguration());
        modelBuilder.ApplyConfiguration(new NeighborhoodInsightConfiguration());
        modelBuilder.ApplyConfiguration(new NeighborhoodInsightEndorsementConfiguration());

        // The extensible catalogues, seeded by contributors at startup.
        modelBuilder.ApplyConfiguration(new AccommodationTypeConfiguration());
        modelBuilder.ApplyConfiguration(new HousingAmenityConfiguration());

        // The safety surface.
        modelBuilder.ApplyConfiguration(new HousingReportConfiguration());

        // The measured record.
        modelBuilder.ApplyConfiguration(new HousingAnalyticsConfiguration());
    }
}
