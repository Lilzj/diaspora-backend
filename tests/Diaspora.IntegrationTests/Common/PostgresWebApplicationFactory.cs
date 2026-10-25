using Diaspora.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.IntegrationTests.Common;

/// <summary>
/// The financial movement host, over real Postgres.
/// <para>
/// <b>For the races SQLite cannot run.</b> On SQLite a unique violation leaves the transaction usable
/// and every writer is serialised, so a test of "two conversions race the last of the pot" or "two
/// deliveries reverse one award" passes whatever the code does. Here the database is the one named by
/// <see cref="PostgresTestDatabase"/>, registered the way <c>AddPersistence</c> registers it in a
/// deployment, retries included, so the transaction behaviour is the deployment's.
/// </para>
/// <para>
/// The schema comes from the migrations already applied to that database: <c>EnsureCreated</c> finds
/// tables and does nothing, and the reference-data seeders run as they would after a deployment's
/// migration job.
/// </para>
/// </summary>
public sealed class PostgresWebApplicationFactory : FinancialMovementWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            RemoveDbContextRegistrations(services);

            var connectionString = PostgresTestDatabase.ConnectionString
                ?? throw new InvalidOperationException(PostgresTestDatabase.SkipReason);

            services.AddDbContext<DiasporaDbContext>(options =>
                options.UseNpgsql(connectionString, npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(DiasporaDbContext).Assembly.FullName);
                    npgsql.EnableRetryOnFailure(3);
                }));
        });
    }
}
