using Diaspora.IntegrationTests.Common;
using Diaspora.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// A test host closes its database last.
/// <para>
/// <b>xUnit v2 knows only <c>Dispose()</c> on a fixture, and that is where the host stops.</b> Whatever
/// a factory closes before calling base is therefore closed under a host that is still running, with
/// its background loops still querying. <c>SqliteConnection.Close</c> is not safe against that: it
/// walks the connection's commands while a loop adds to them, and what it throws comes out of the
/// fixture's cleanup. Every test had passed by then, so CI went red on a run that reported
/// <c>Failed: 0</c>, and the API tests after it never ran.
/// </para>
/// <para>
/// <b>Asserted from inside the shutdown, not by racing it.</b> The race took one disposal in thirty
/// under a loop written to provoke it. A hosted service that reads the database as it stops needs no
/// luck: it is only answered if the database is still there.
/// </para>
/// </summary>
public sealed class TestHostShutdownTests
{
    [Fact]
    public Task The_shared_connection_outlives_the_host() =>
        DatabaseOutlivesTheHostAsync(new CustomWebApplicationFactory());

    [Fact]
    public Task The_real_time_database_outlives_the_host() =>
        DatabaseOutlivesTheHostAsync(new RealtimeWebApplicationFactory());

    [Fact]
    public Task The_concurrent_database_outlives_the_host() =>
        DatabaseOutlivesTheHostAsync(new ConcurrentWebApplicationFactory());

    private static async Task DatabaseOutlivesTheHostAsync(CustomWebApplicationFactory factory)
    {
        var probe = new ShutdownProbe();

        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(probe);
                services.AddHostedService<ProbeOnStop>();
            }));

        using (var scope = host.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

            // A table rather than SELECT 1: closing the keep-alive of a shared-cache database leaves
            // an empty one behind, which would answer SELECT 1 and hide the very thing being asked.
            await database.Database.ExecuteSqlRawAsync("CREATE TABLE shutdown_probe (value INTEGER)");
        }

        factory.Dispose();

        probe.Failure.Should().BeNull("the host was still running, so its database must still be there");
        probe.Answered.Should().BeTrue("the probe reads the database as the host stops");
    }

    private sealed class ShutdownProbe
    {
        public bool Answered { get; set; }

        public Exception? Failure { get; set; }
    }

    private sealed class ProbeOnStop(IServiceScopeFactory scopes, ShutdownProbe probe) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var database = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

                await database.Database.ExecuteSqlRawAsync("DELETE FROM shutdown_probe", cancellationToken);

                probe.Answered = true;
            }
#pragma warning disable CA1031 // Reported by the test: a throw here would only stop the host early.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                probe.Failure = exception;
            }
        }
    }
}
