using Diaspora.Modules.Location.Persistence.Configurations;
using Diaspora.Persistence.Modules;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Location.Persistence;

/// <summary>
/// Adds the Location module's tables to the shared <c>DiasporaDbContext</c>.
/// <para>
/// Eight tables in the module's own <c>location</c> schema, which the context verifies at
/// startup. The schema boundary matters here for a reason particular to geography: the
/// catalogue is the one dataset on the platform that a later deployment may want to replace
/// wholesale with a licensed gazetteer, and a bulk swap is a schema-level operation only if
/// nothing else lives in the schema.
/// </para>
/// </summary>
public sealed class LocationDatabaseContributor : IDatabaseContributor
{
    public string Module => LocationModuleMetadata.Name;

    public string Schema => LocationModuleMetadata.Schema;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Catalogue
        modelBuilder.ApplyConfiguration(new CountryConfiguration());
        modelBuilder.ApplyConfiguration(new AdministrativeAreaConfiguration());

        // Member geography — owns saved_locations and recent_locations
        modelBuilder.ApplyConfiguration(new LocationProfileConfiguration());

        // Residence history — owns country_expertise
        modelBuilder.ApplyConfiguration(new CountryExperienceConfiguration());

        // The projection of Trust's decisions
        modelBuilder.ApplyConfiguration(new VerifiedResidencyConfiguration());

        // The "when the app is open" setting and the last area it resolved to (21 Oct 2026)
        modelBuilder.ApplyConfiguration(new MemberPresenceConfiguration());

        // The world list of towns, for reading a place out of a search (5 Nov 2026)
        modelBuilder.ApplyConfiguration(new WorldTownConfiguration());
    }
}
