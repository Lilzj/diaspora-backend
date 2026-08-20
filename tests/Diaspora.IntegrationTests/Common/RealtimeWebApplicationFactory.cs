using Diaspora.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.IntegrationTests.Common;

/// <summary>
/// The same host, over a SQLite database that more than one connection can reach at once.
/// <para>
/// <b>Real-time is the first thing in this suite that holds an HTTP request open.</b> Long polling
/// keeps a GET parked on the server for as long as the client is connected, and every request in
/// this platform — that one included — validates its token by reading the session table. So while a
/// member is connected there is permanently one open reader, and the next request the test makes
/// opens a second. Against the shared single connection every other suite uses, SQLite answers
/// <c>SQLITE_MISUSE: unable to delete/modify user-function due to active statements</c> and the
/// request 500s.
/// </para>
/// <para>
/// <b>Nothing about that is a product defect, and the fix is not to work around it in the
/// product.</b> A deployment runs Postgres with a connection pool; concurrent requests are the
/// ordinary case. What is wrong is the test host's single connection, which exists because an
/// in-memory SQLite database is destroyed when its last connection closes. A named shared-cache
/// database keeps that property while permitting several connections: this factory holds one open
/// for the fixture's lifetime purely to keep the database alive, and hands EF a connection
/// <em>string</em> so every context opens its own.
/// </para>
/// <para>
/// The name carries a fresh identifier per instance so two fixtures running side by side do not
/// share a schema — shared-cache databases are global to the process by name.
/// </para>
/// </summary>
public sealed class RealtimeWebApplicationFactory : CustomWebApplicationFactory
{
    private readonly string _connectionString =
        $"Data Source=realtime-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    private SqliteConnection? _keepAlive;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            // Strips the base factory's single-connection registration. Registered after it, so
            // this one wins on the DbContextOptions the host finally resolves.
            RemoveDbContextRegistrations(services);

            // Held open and never used for a query. An in-memory SQLite database lives exactly as
            // long as its last connection, so without this the schema would vanish between the
            // request that created it and the next one.
            _keepAlive = new SqliteConnection(_connectionString);
            _keepAlive.Open();

            services.AddDbContext<DiasporaDbContext>(options => options.UseSqlite(_connectionString));
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
