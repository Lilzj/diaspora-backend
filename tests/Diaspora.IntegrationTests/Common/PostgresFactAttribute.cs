using Xunit;

namespace Diaspora.IntegrationTests.Common;

/// <summary>
/// Where the suite's Postgres runs point, if anywhere.
/// <para>
/// <b>Off unless a connection string is set.</b> The suite runs on SQLite so it needs no database
/// server (see <see cref="CustomWebApplicationFactory"/>), and SQLite cannot show the behaviour the
/// Postgres tests exist for: a unique violation aborting a transaction, two writers on one row with
/// real isolation. Those tests run where <c>DIASPORA_TEST_POSTGRES</c> names a migrated database,
/// such as a Neon branch made for the run, and are skipped everywhere else, CI included.
/// </para>
/// <para>
/// The database must already carry every migration. The host never migrates it, and it writes test
/// members and money movements into it, so it must never be a deployment's database.
/// </para>
/// </summary>
public static class PostgresTestDatabase
{
    public const string EnvironmentVariable = "DIASPORA_TEST_POSTGRES";

    public const string SkipReason =
        "Set " + EnvironmentVariable + " to the connection string of a migrated, disposable Postgres "
        + "database to run this test. It is skipped otherwise.";

    public static string? ConnectionString { get; } =
        Environment.GetEnvironmentVariable(EnvironmentVariable) is { Length: > 0 } value ? value : null;
}

/// <summary>A test that needs real Postgres: skipped unless <see cref="PostgresTestDatabase"/> names one.</summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (PostgresTestDatabase.ConnectionString is null)
        {
            Skip = PostgresTestDatabase.SkipReason;
        }
    }
}
