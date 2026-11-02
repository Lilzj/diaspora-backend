using System.Data.Common;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Persistence;
using Diaspora.Platform.Modules.Seeding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Diaspora.IntegrationTests.Common;

/// <summary>
/// Hosts the real API against an isolated in-memory SQLite database.
/// <para>
/// SQLite rather than Testcontainers so the suite runs on any machine, in any CI job, with
/// no Docker daemon and no container pull — the difference between tests that run on every
/// commit and tests that run when someone remembers. The EF mappings deliberately avoid
/// provider-specific column types, so the same model works on both; a Postgres-flavoured
/// run remains available through Testcontainers when provider-specific behaviour needs
/// checking.
/// </para>
/// <para>
/// The connection is held open for the factory's lifetime: an in-memory SQLite database
/// is destroyed the moment its last connection closes.
/// </para>
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Api.Program>
{
    /// <summary>
    /// Signing key for the Testing environment. The options validator refuses to start
    /// outside Development without one, which is exactly the behaviour under test.
    /// </summary>
    public const string TestSigningKey = "diaspora-integration-test-signing-key-32-bytes-plus";

    /// <summary>The one-time codes delivered during a test run.</summary>
    public CapturedOtpCodes OtpCodes { get; } = new();

    /// <summary>The email-change mail sent during a test run.</summary>
    public CapturedEmailChangeMail EmailChangeMail { get; } = new();

    private readonly Lock _schemaLock = new();

    private DbConnection? _connection;
    private bool _schemaCreated;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:Jwt:SigningKey"] = TestSigningKey,
                ["Identity:Jwt:Issuer"] = "https://identity.diaspora.test",
                ["Identity:Jwt:Audience"] = "https://api.diaspora.test",
                ["Identity:Jwt:AccessTokenMinutes"] = "15",

                // Kept low so tests can exhaust budgets without long loops.
                ["Identity:Otp:MaxVerificationAttempts"] = "3",
                ["Identity:Otp:MaxResends"] = "2",
                ["Identity:Otp:ResendCooldownSeconds"] = "0",
                ["Identity:Otp:MaxRequestsPerWindow"] = "20",
                ["Identity:Lockout:MaxFailedAttempts"] = "3",

                // Faster hashing; the algorithm is exercised by the unit suite.
                ["Identity:Password:HashIterations"] = "10000",

                // The suite drives dozens of sign-ins from one client in a second — the
                // exact shape the limiter exists to refuse. It is exercised in isolation by
                // the rate-limiting tests; here it would only make the identity tests flaky.
                // The options validator permits this in the Testing environment and nowhere
                // deployed.
                ["RateLimiting:Enabled"] = "false",

                // The outbox itself stays on — every test in this suite exercises the real
                // durable write path, and turning it off would make the suite prove the
                // pre-B1.1 behaviour still works rather than the new one.
                //
                // Its background dispatcher does not. Two reasons, and both are about the test
                // host rather than the component: the whole suite shares one in-memory SQLite
                // connection, and a background loop querying it while a request is mid-command
                // is a concurrent use of a connection SQLite will not allow. Second, a test
                // that asserts on a consumer's side effect after a timer happens to have fired
                // is a flaky test wearing a passing badge. Tests that need delivery call
                // DrainOutboxAsync and get a deterministic, fully drained queue.
                ["Platform:Outbox:Dispatch:Enabled"] = "false",

                // B13's four loops are off for the same two reasons the outbox dispatcher is: the
                // suite shares one in-memory SQLite connection, which a background loop querying it
                // mid-command would be a concurrent use of; and a test asserting on a channel's side
                // effect after a timer happens to have fired is a flaky test wearing a passing badge.
                // Tests drive the delivery runner and the sweeps directly.
                ["Notifications:EnableDeliverySweep"] = "false",
                ["Notifications:EnableDigestSweep"] = "false",
                ["Notifications:EnableRetentionSweep"] = "false",
                ["Notifications:EnableCounterReconciliation"] = "false",
                ["Platform:Outbox:Retention:Enabled"] = "false",

                // B13's real-time runtime stays on: the hub, its authentication and its fan-out are
                // under test, and a suite that disabled them would assert the disabled behaviour.
                //
                // Detailed hub errors are on here and nowhere deployed. A hub exception is otherwise
                // returned to the client as "An unexpected error occurred", which in a failing test
                // means a 500 with no indication of which line threw — the exact opposite of what a
                // test run is for. The option's own documentation says why a deployment must not do
                // this: the message can carry an identifier or a member's own data.
                ["Notifications:Realtime:EnableDetailedErrors"] = "true",

                // B14's four loops are off for the same two reasons B13's are: the suite shares one
                // in-memory SQLite connection, which a background loop querying it mid-command would
                // be a concurrent use of; and a test asserting on a profile after a timer happens to
                // have fired is a flaky test wearing a passing badge. Tests drive the sweeps
                // directly through their public RunOnceAsync.
                ["Recommendations:EnableProfileSweep"] = "false",
                ["Recommendations:EnableRetentionSweep"] = "false",
                ["Recommendations:EnableAnalyticsSweep"] = "false",
                ["Recommendations:EnableCandidateSweep"] = "false",

                // B8.2's retirement sweep is off for the same two reasons every other background
                // loop is: the suite shares one in-memory SQLite connection, which a loop querying
                // it mid-command would be a concurrent use of; and a test asserting on a notice
                // after a timer happens to have fired is a flaky test wearing a passing badge.
                // Tests drive NoticeExpirySweep.RunOnceAsync directly.
                ["Intelligence:EnableExpirySweep"] = "false",

                // And its cache is off, because a cached country intelligence page would make a
                // test that publishes a notice and then reads the country page assert against the
                // page before it.
                ["Intelligence:EnableCaching"] = "false",

                // Attempt recording is off in production and on here: the suite asserts against
                // the attempt log, and a table nothing ever writes to is a table nobody notices
                // has stopped working.
                ["Platform:Outbox:Dispatch:RecordDeliveryAttempts"] = "true",

                // The last six loops that could not be switched off, and now can.
                //
                // Every entry above this one was added by the phase that owned the sweep. These six
                // were the remainder: B5's two, B6's, B12's two and B1.0.2's receipt sweep, all of
                // which ran unconditionally in this host because no switch existed. DEBT-049
                // recorded the consequence three times — four tests failing with a 500 on register
                // in one full run that passed 615/615 four other times, and one run that aborted
                // with an Internal CLR error — and each observation is the same phenomenon: a
                // background loop querying the one shared in-memory SQLite connection while a
                // request is mid-command is a concurrent use SQLite will not allow.
                //
                // The reason stated at the top of this file for the outbox dispatcher applied
                // identically to all six from the day each shipped. PSR-1 Final Certification added
                // the switches; this is what they were for.
                ["Media:Upload:EnableSweeper"] = "false",
                // Tests file rows by running AreaFilingSweep.RunOnceAsync directly.
                ["Location:Spatial:EnableAreaFilingSweep"] = "false",

                // **A timeout meant for a member's screen is not a timing assertion.**
                // The nearby fan-out gives each vertical 2.5 seconds and a counter 1.5 so one bad
                // provider cannot hold a browse screen. Under the full suite — hundreds of classes
                // sharing one machine and one SQLite connection — providers overrun that and the
                // group comes back empty, which read as "the alerts layer is broken" in four tests
                // that pass alone (CommunityAlertLifecycleTests, NearbyUpdatePayloadTests,
                // LocationModuleTests). The budgets are raised here so a slow machine cannot fake a
                // missing feature; that the budget is enforced at all is
                // LocationAggregatorConcurrencyTests' business, with budgets of its own.
                ["Location:Spatial:NearbyProviderTimeoutMilliseconds"] = "30000",
                ["Location:Spatial:ResourceCounterTimeoutMilliseconds"] = "30000",

                // The suite never calls a third party. The safety counts' police source answers
                // "Disabled" here; its parsing is tested against a fake transport.
                ["GeographicIntelligence:PoliceUk:Enabled"] = "false",
                ["Media:Processing:EnableWorker"] = "false",
                ["Community:EnableExpirySweep"] = "false",
                ["CommunityLife:EnableLifecycleSweep"] = "false",
                ["CommunityLife:EnableMembershipSweep"] = "false",

                // B12.1. Off for the same reason as the two above — DEBT-049: a background loop
                // writing through the one shared SQLite connection races every test in the suite.
                // The series tests drive expansion through the create and publish handlers, which
                // is the path a real deployment uses first anyway.
                ["CommunityLife:EnableSeriesExpansionSweep"] = "false",
                ["Platform:Transactions:EnableReceiptRetentionSweep"] = "false",

                // B18's two loops, off for the same two reasons every loop above is — DEBT-049: the
                // suite shares one in-memory SQLite connection, which a background loop querying it
                // mid-command would be a concurrent use of; and a test asserting on a membership
                // after a timer happens to have fired is a flaky test wearing a passing badge. Both
                // sweeps expose a public RunOnceAsync, and the tests drive that.
                ["CommunityProgramme:EnableLifecycleSweep"] = "false",
                ["CommunityProgramme:EnableReconciliationSweep"] = "false",

                // B15.1's three loops, off for the same two reasons. Every one of them exposes a
                // public RunOnceAsync and the tests drive that — including the recurring-donation
                // worker, whose whole point is that two instances must not charge the same schedule,
                // and which is therefore raced deliberately rather than left to a timer.
                ["Financial:EnableCampaignSweep"] = "false",
                ["Financial:EnableRecurringDonationSweep"] = "false",
                ["Financial:EnableMembershipBindingReconciliation"] = "false",

                // The reconciliation grace period exists so ordinary outbox latency is not mistaken
                // for a failed activation. A test drives the sweep by hand the instant after it has
                // arranged the failure, so the grace period would hide the very row it seeded — and
                // the test would pass by finding nothing, which is the shape of a green test that
                // proves nothing.
                ["Financial:MembershipBindingReconciliationAfterMinutes"] = "0",

                // Campaign review is on by default in production, because a campaign is a request
                // for strangers' money in somebody's name. It is off here because publishing under
                // review needs a staff token and this suite has none (DEBT-031), and a suite that
                // could not publish a campaign could not test anything downstream of publication.
                // The gate itself is asserted separately, against a host that leaves it on.
                ["Financial:RequireCampaignReview"] = "false",

                // B19's three loops, off for the same two reasons every loop above is — DEBT-049:
                // this suite shares one in-memory SQLite connection, and a projection querying it
                // mid-command is a concurrent use SQLite will not allow; and a test asserting on a
                // figure after a timer happens to have fired is a flaky test wearing a passing
                // badge. The projection, the retention sweep and the backfill worker each expose a
                // public RunOnceAsync and the tests drive that.
                ["Analytics:EnableProjectionSweep"] = "false",
                ["Analytics:EnableRetentionSweep"] = "false",

                // B22's one loop, off for the same two reasons every loop above is — DEBT-049: this
                // suite shares one in-memory SQLite connection, and a sweep querying it mid-command
                // is a concurrent use SQLite will not allow. The sweep is driven directly where a
                // test needs it.
                ["Integrations:EnableMaintenanceSweep"] = "false",

                // Step 3f (RWD-14, CS-4b). Risk assessment is off in every deployment until it is
                // turned on, and on here, so the suite proves the path a deployment takes once it is:
                // evidence recorded, a High member's points held and publishing paused. Its sweep is
                // off for the same two reasons every loop above is (DEBT-049), and is driven directly.
                ["Trust:EnableRiskAssessment"] = "true",
                ["Trust:EnableRiskSweep"] = "false",

                // The loops that were left, found by measuring rather than by reading. A host left idle
                // for twenty minutes was still querying the shared connection: Automation six times
                // every thirty seconds, Guides every minute, Marketplace, Housing and Financial every
                // ten or fifteen, and Creator, Providers and Conversations shortly after start. The
                // same two reasons as every loop above (DEBT-049). Automation had no switch at all;
                // Every_background_loop_can_be_switched_off had been fooled by the word "Runnable".
                //
                // Left on: the Operations workers, which queried nothing in those twenty minutes.
                ["AutomationDefinition:EnableSweep"] = "false",
                ["Conversations:EnableAttentionSweep"] = "false",
                ["Creator:EnableLifecycleSweep"] = "false",
                ["Guides:EnableScheduledPublisher"] = "false",
                ["Housing:EnableBackgroundSweeps"] = "false",
                ["Marketplace:EnableBackgroundSweeps"] = "false",
                ["Providers:EnableSweeper"] = "false",

                // Every other Financial sweep, for every host rather than only the money-movement
                // one. FinancialSweep has a switch on each of them for exactly this reason, and tests
                // drive RunOnceAsync directly, which the switches do not touch.
                ["Financial:EnableWithdrawalSweep"] = "false",
                ["Financial:EnableRewardConversionReleaseSweep"] = "false",
                ["Financial:EnableRewardVestingSweep"] = "false",
                ["Financial:EnableExpiryNoticeSweep"] = "false",
                ["Financial:EnableReconciliationSweep"] = "false",
                ["Financial:EnablePayoutReconciliationSweep"] = "false",
                ["Financial:EnablePaymentExpirySweep"] = "false",
                ["Financial:EnablePlacementSweep"] = "false",
                ["Financial:EnableSubscriptionSweep"] = "false",
                ["Financial:EnableCreditExpirySweep"] = "false",
                ["Financial:EnableRevenueRollupSweep"] = "false",
                ["Financial:EnableGivingReconciliationSweep"] = "false",

                // The OAuth allow-lists. Empty is the correct production default — a deployment
                // that has configured neither can start no authorization and make no outbound call
                // — and a suite that left them empty would prove only the refusal. These two
                // entries are what let the exchange, the calendar proxy and the isolation tests
                // exercise the path a configured deployment takes.
                ["Integrations:AllowedRedirectUris:0"] = "https://app.diaspora.test/oauth/callback",
                ["Integrations:AllowedRedirectUris:1"] = "diaspora://oauth/callback",
                ["Integrations:AllowedOutboundHosts:0"] = "oauth.provider.test",
                ["Integrations:AllowedOutboundHosts:1"] = ".googleapis.com",

                ["ConnectionStrings:DefaultConnection"] = "DataSource=:memory:"
            });
        });

        builder.ConfigureServices(services =>
        {
            RemoveDbContextRegistrations(services);

            // Substitute the SMS gateway, exactly as a production deployment substitutes a
            // real provider for the development logger.
            services.AddSingleton(OtpCodes);
            services.Replace(ServiceDescriptor.Scoped<IOtpDeliveryService, CapturingOtpDeliveryService>());

            // The same substitution for the email-change mail, so the flow can be followed from the
            // request to the link without an email transport. Verification keeps none: its tests
            // assert the unconfigured answer.
            services.AddSingleton(EmailChangeMail);
            services.Replace(ServiceDescriptor.Scoped<IEmailChangeMailer, CapturingEmailChangeMailer>());

            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<DiasporaDbContext>(options => options.UseSqlite(_connection));
        });
    }

    /// <summary>
    /// Creates the schema, once, against the running server's provider.
    /// <para>
    /// Deliberately not done in <c>ConfigureServices</c> or in a <c>CreateHost</c> override.
    /// The API uses the minimal hosting model, where the provider is not resolvable at
    /// either point: building a temporary one disposes itself, and the host builder's
    /// provider is still deferred. Reading <see cref="WebApplicationFactory{T}.Services"/>
    /// after the server has started is the one place a real provider exists.
    /// </para>
    /// </summary>
    public void EnsureDatabaseCreated()
    {
        lock (_schemaLock)
        {
            if (_schemaCreated) return;

            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();
            dbContext.Database.EnsureCreated();

            SeedModuleReferenceData();

            _schemaCreated = true;
        }
    }

    /// <summary>
    /// Re-runs every module's reference-data seeder now that the schema exists.
    /// <para>
    /// A module that ships reference data registers a hosted service to seed it, and that
    /// service runs when the host starts — which here is before
    /// <see cref="EnsureDatabaseCreated"/> has created the schema, so its first attempt finds
    /// no tables and logs a failure. Re-invoking is safe because every seeder is idempotent by
    /// contract, and it is the same call a deployment whose migrations are applied by a
    /// separate job would make.
    /// </para>
    /// <para>
    /// <b>Discovered rather than listed, since PSR-1, and the list is why.</b> This method used to
    /// name twelve seeders one at a time, each behind a <c>using</c> on a module's internal hosting
    /// namespace. A thirteenth module that forgot to add itself did not fail one test — its
    /// catalogue stayed empty and <em>every</em> test in the suite died at host start, with an
    /// error naming neither the module nor the seeder. B8 recorded that, B8.1 paid it again, and
    /// the platform restated the registration contract from memory in four more phases.
    /// <see cref="ReferenceDataSeedRunner"/> now resolves whatever is registered and runs it in
    /// module order, so a new module's catalogue arrives here with no edit at all.
    /// </para>
    /// </summary>
    private void SeedModuleReferenceData()
    {
        using var scope = Services.CreateScope();

        scope.ServiceProvider
            .GetRequiredService<ReferenceDataSeedRunner>()
            .SeedAllAsync()
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>
    /// Strips the Npgsql registration the host added, along with the internal EF service
    /// provider it cached. Leaving either behind makes EF resolve the wrong provider.
    /// <para>
    /// Protected so a derived factory that needs a different SQLite arrangement can strip this
    /// factory's registration in turn. <see cref="RealtimeWebApplicationFactory"/> is the one that
    /// does, and it documents why a single shared connection cannot serve it.
    /// </para>
    /// </summary>
    protected static void RemoveDbContextRegistrations(IServiceCollection services)
    {
        var doomed = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(DbContextOptions<DiasporaDbContext>) ||
                descriptor.ServiceType == typeof(DbContextOptions) ||
                descriptor.ServiceType == typeof(DiasporaDbContext) ||
                (descriptor.ServiceType.FullName?.Contains(
                    "IDbContextOptionsConfiguration", StringComparison.Ordinal) ?? false))
            .ToList();

        foreach (var descriptor in doomed)
        {
            services.Remove(descriptor);
        }
    }

    /// <summary>
    /// Stops the host, then closes its database, in that order.
    /// <para>
    /// <b>xUnit v2 disposes a fixture through <c>Dispose()</c>, and base stops the host from inside
    /// it.</b> Closing first therefore closed the database under a running host. Some loops have no
    /// switch to turn them off here, and <c>SqliteConnection.Close</c> walks its commands with nothing
    /// guarding them, so a query landing on that moment threw out of the fixture's cleanup: a failed
    /// CI job on a run reporting <c>Failed: 0</c>.
    /// </para>
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        try
        {
            base.Dispose(disposing);
        }
        finally
        {
            if (disposing)
            {
                CloseDatabase();
            }
        }
    }

    /// <summary>
    /// Releases whatever keeps this factory's database alive. Called once the host has stopped, so a
    /// derived factory holding a connection of its own releases it here rather than in
    /// <see cref="Dispose(bool)"/>, where it would go before the host does.
    /// </summary>
    protected virtual void CloseDatabase()
    {
        _connection?.Dispose();
        _connection = null;
    }
}
