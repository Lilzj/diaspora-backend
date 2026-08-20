using Diaspora.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.IntegrationTests.Common;

/// <summary>
/// The same host, over a SQLite database more than one request can reach at once.
/// <para>
/// <b>A concurrency test cannot run against the shared single connection every other suite uses, and
/// the failure it produces looks exactly like a product defect.</b> Two requests arriving together
/// both validate their token by reading the session table, and against one shared
/// <c>SqliteConnection</c> the driver throws
/// <c>ArgumentOutOfRangeException</c> from <c>SqliteConnection.RemoveCommand</c> — a 500, raised in
/// the authentication middleware, before the request reaches any module. B18's first concurrency test
/// produced precisely that, and the answer is the same one <see cref="RealtimeWebApplicationFactory"/>
/// gives: the test host is wrong, not the platform. A deployment runs Postgres with a pool, where
/// concurrent requests are the ordinary case.
/// </para>
/// <para>
/// Identical in mechanism to the real-time factory — a named shared-cache database, one connection
/// held open purely to keep it alive, and a connection <em>string</em> handed to EF so every context
/// opens its own. Kept as a separate type rather than reusing that one because the two are used for
/// different reasons, and a test that borrowed "the real-time factory" to prove something about
/// idempotency would read as a mistake.
/// </para>
/// </summary>
public sealed class ConcurrentWebApplicationFactory : CustomWebApplicationFactory
{
    private readonly string _connectionString =
        $"Data Source=concurrent-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    private SqliteConnection? _keepAlive;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            // Strips the base factory's single-connection registration. Registered after it, so this
            // one wins on the DbContextOptions the host finally resolves.
            RemoveDbContextRegistrations(services);

            // Held open and never used for a query: an in-memory SQLite database lives exactly as
            // long as its last connection.
            _keepAlive = new SqliteConnection(_connectionString);
            _keepAlive.Open();

            services.AddDbContext<DiasporaDbContext>(
                options => options.UseSqlite(_connectionString));
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _keepAlive?.Dispose();
            _keepAlive = null;
        }

        base.Dispose(disposing);
    }
}
