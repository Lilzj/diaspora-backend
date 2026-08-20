using Diaspora.Persistence.Modules;
using Diaspora.Platform.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Diaspora.Persistence;

/// <summary>
/// Supplies a <see cref="DiasporaDbContext"/> to the EF Core command-line tooling.
/// <para>
/// Without this, <c>dotnet ef</c> would boot the API host to find a context — dragging in
/// configuration, secrets and the whole DI graph just to scaffold a migration. The
/// connection string here never connects during scaffolding; it only names the provider so
/// the generated SQL is PostgreSQL's.
/// </para>
/// <para>
/// It does, however, discover the database contributors, and this is essential rather than
/// incidental: a scaffolded migration reflects whatever entities the model contains, and a
/// model built without the contributors would omit every module's tables. Run
/// <c>dotnet ef</c> with <c>--startup-project src/Diaspora.Api</c> so the module assemblies
/// are in the dependency context for the scan to find. That is what lets a module get its
/// own migration without Persistence referencing it.
/// </para>
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DiasporaDbContext>
{
    private const string ScaffoldingConnectionString =
        "Host=localhost;Port=5432;Database=diaspora;Username=postgres;Password=postgres";

    public DiasporaDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DIASPORA_MIGRATIONS_CONNECTION")
            ?? ScaffoldingConnectionString;

        var options = new DbContextOptionsBuilder<DiasporaDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(DiasporaDbContext).Assembly.FullName))
            .Options;

        var databaseContributors = ModuleAssemblyScanner
            .CreateAll<IDatabaseContributor>(ModuleAssemblyScanner.FindPlatformAssemblies());

        return new DiasporaDbContext(
            options,
            currentUser: null,
            clock: null,
            eventDispatcher: null,
            databaseContributors: databaseContributors);
    }
}
