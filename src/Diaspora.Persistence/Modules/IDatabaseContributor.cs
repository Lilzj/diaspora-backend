using Microsoft.EntityFrameworkCore;

namespace Diaspora.Persistence.Modules;

/// <summary>
/// Implemented by a module to add its own entities to the shared <c>DiasporaDbContext</c>.
/// <para>
/// Deliberately declared here and not in the platform kernel. Everything else a module
/// contributes — permissions, gates, policies, endpoints, telemetry — is expressible
/// without a persistence technology, and the kernel should not acquire an EF Core
/// dependency to serve the one contributor that genuinely needs one. A module that stores
/// nothing never references this project.
/// </para>
/// <para>
/// One <c>DbContext</c> across modules is the modular-monolith trade: it buys a single
/// transaction and a single connection pool, and it costs the ability to migrate one
/// module independently. Table ownership is enforced by convention instead — each module
/// keeps its tables in its own schema, so extracting one later is a schema move.
/// </para>
/// </summary>
public interface IDatabaseContributor
{
    /// <summary>The owning module's name.</summary>
    string Module { get; }

    /// <summary>
    /// The schema this module's tables live in. Enforced at startup: a module writing
    /// into another module's schema is a boundary violation the database will happily
    /// allow and nobody will notice for a year.
    /// </summary>
    string Schema { get; }

    /// <summary>
    /// Applies the module's entity configurations. Called from
    /// <c>DiasporaDbContext.OnModelCreating</c>, after the host's own configurations.
    /// </summary>
    void Configure(ModelBuilder modelBuilder);
}
