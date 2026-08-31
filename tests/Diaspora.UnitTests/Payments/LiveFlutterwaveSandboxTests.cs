using Diaspora.Payments.Flutterwave;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Payments;

/// <summary>
/// The Flutterwave adapter against a real sandbox account.
/// <para>
/// Skipped unless <c>FLUTTERWAVE_SECRET_KEY</c> is in the environment, so it is inert in CI and on
/// any machine without credentials. Run it deliberately:
/// </para>
/// <code>
/// FLUTTERWAVE_SECRET_KEY=FLWSECK_TEST-... dotnet test tests/Diaspora.UnitTests \
///   --filter FullyQualifiedName~LiveFlutterwaveSandboxTests
/// </code>
/// <para>
/// <b>Nothing here moves money.</b> Reading the transfers list proves the credential, the transport
/// and — the part the stubbed tests cannot — that Flutterwave's payload still has the shape the
/// reconciliation mapper reads. Creating a transfer would need a funded balance and a real
/// beneficiary, and a test that moved money as a side effect of running the suite is not one
/// anybody should be able to run by accident.
/// </para>
/// <para>
/// <b>This is what turns the Flutterwave capability rows from <c>Unknown</c> into something a
/// deployment may set to <c>Supported</c>.</b> Until it has been run against a real account, the
/// adapter's payloads are written from documentation and never executed, and the router refuses to
/// use the provider at all.
/// </para>
/// </summary>
public sealed class LiveFlutterwaveSandboxTests
{
    /// <summary>
    /// A guard rather than an assertion about Flutterwave: a live secret key here would mean
    /// somebody pointed the suite at production.
    /// </summary>
    [LiveFlutterwave]
    public void The_live_suite_refuses_to_run_against_a_production_key()
    {
        Settings().IsTestMode.Should().BeTrue(
            "FLUTTERWAVE_SECRET_KEY must be a test key; point this suite at a sandbox account");
    }

    /// <summary>
    /// The credential works, the transfers list is readable, and its rows still parse into the
    /// records reconciliation compares against. A 401 here means the key is wrong or rolled.
    /// </summary>
    [LiveFlutterwave]
    public async Task The_reconciliation_provider_can_read_the_transfers_list()
    {
        var settings = Settings();

        var provider = new FlutterwavePayoutReconciliationProvider(
            new StaticOptions(settings),
            new HttpClient(),
            NullLogger<FlutterwavePayoutReconciliationProvider>.Instance);

        var to = DateTimeOffset.UtcNow;
        var from = to.AddDays(-30);

        // Throws on a non-2xx, which is the assertion: an unreadable list must not look like an
        // empty one, because an empty one reads as "nothing unaccounted for".
        var records = await provider.FetchAsync(from, to);

        records.Should().NotBeNull();

        // A brand-new sandbox has no transfers, so an empty list is a pass. What is being proved is
        // that the call authenticated and every row that came back mapped without throwing.
        foreach (var record in records)
        {
            record.Provider.Should().Be(settings.Provider);
            record.Status.Should().BeOneOf("paid", "failed", "reversed");
            record.Currency.Should().HaveLength(3);
            record.OccurredAt.Should().BeOnOrAfter(from).And.BeOnOrBefore(to);
        }
    }

    private static FlutterwaveOptions Settings() => new()
    {
        SecretKey = Environment.GetEnvironmentVariable("FLUTTERWAVE_SECRET_KEY") ?? string.Empty
    };

    private sealed class StaticOptions(FlutterwaveOptions value)
        : IOptionsMonitor<FlutterwaveOptions>
    {
        public FlutterwaveOptions CurrentValue => value;

        public FlutterwaveOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<FlutterwaveOptions, string?> listener) => null;
    }
}

/// <summary>Runs only when <c>FLUTTERWAVE_SECRET_KEY</c> is set.</summary>
public sealed class LiveFlutterwaveAttribute : FactAttribute
{
    public LiveFlutterwaveAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FLUTTERWAVE_SECRET_KEY")))
        {
            Skip = "Set FLUTTERWAVE_SECRET_KEY to run this against a live Flutterwave sandbox account.";
        }
    }
}
