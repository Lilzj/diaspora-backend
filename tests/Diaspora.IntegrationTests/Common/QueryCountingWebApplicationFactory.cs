using System.Data.Common;
using Diaspora.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.IntegrationTests.Common;

/// <summary>
/// A host that counts every command the database receives, so a budget can be asserted where the
/// request is served rather than where the repository is called.
/// <para>
/// <b>BBL-005 asks for the feed's bound at the endpoint, and that is a different number.</b> The
/// repository budget added with the first half of this finding answers 1, because inside a single
/// repository call there is nothing else to spend a query on. A request spends queries on the
/// viewer's context, authorization, the feed provider, hydration and the trust levels — and the
/// defect the finding was about lived in exactly that space between them.
/// </para>
/// <para>
/// <b>Its own factory, not <c>WithWebHostBuilder</c> on a shared one</b>, for the reason
/// <see cref="GrantRefusingWebApplicationFactory"/> records: the base factory opens its in-memory
/// database inside <c>ConfigureServices</c>, so deriving a host from a shared fixture swaps that
/// field for a new empty database and every later test in the collection fails reading a schema
/// that is no longer there.
/// </para>
/// <para>
/// <b>It registers the context again rather than adding an <see cref="IInterceptor"/> service.</b>
/// Registering the counter in DI is the documented way to attach an interceptor and it attaches
/// nothing here — the first version of this factory did exactly that, every budget below read
/// zero, and the only reason that was not mistaken for a very fast feed is that one test asserts
/// the counter counts. Re-registering the context with <c>AddInterceptors</c> puts the counter
/// where the options are built, which is the one place it cannot be missed.
/// </para>
/// </summary>
public sealed class QueryCountingWebApplicationFactory : CustomWebApplicationFactory
{
    private SqliteConnection? _counted;

    /// <summary>The commands counted since the last reset.</summary>
    public QueryCounter Counter { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            // Its own database as well as its own interceptor: the base factory's connection is
            // private to it, and a counted host that shared one would count its neighbours.
            _counted = new SqliteConnection("DataSource=:memory:");
            _counted.Open();

            services.AddDbContext<DiasporaDbContext>(
                options => options.UseSqlite(_counted).AddInterceptors(Counter));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _counted?.Dispose();
            _counted = null;
        }
    }
}

/// <summary>
/// Counts the commands a request sends to the database, and keeps their text.
/// <para>
/// <b>An interceptor rather than the EF log, because a count has to be exact.</b>
/// <c>Microsoft.EntityFrameworkCore.Database.Command</c> is a logging category whose output
/// depends on the configured level and whose text has to be matched;
/// <see cref="DbCommandInterceptor"/> is the supported seam and sees every execution path —
/// reader, scalar and non-query.
/// </para>
/// <para>
/// <b>It records the SQL as well as the count</b>, because "expected 6, found 56" is true and
/// tells nobody which fifty-six. The statements go in the failure message so the loop names
/// itself.
/// </para>
/// </summary>
public sealed class QueryCounter : DbCommandInterceptor
{
    private readonly List<string> _statements = [];
    private readonly Lock _gate = new();

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _statements.Count;
            }
        }
    }

    /// <summary>Starts counting again, so a measurement covers one request and not its setup.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _statements.Clear();
        }
    }

    /// <summary>The statements, numbered, for a failure message somebody can act on.</summary>
    public string Describe()
    {
        lock (_gate)
        {
            return _statements.Count == 0
                ? "(no statements)"
                : string.Join(
                    "\n",
                    _statements.Select((statement, index) => $"  {index + 1}. {Flatten(statement)}"));
        }
    }

    /// <summary>How many of the counted statements read from a table, by its name.</summary>
    public int CountMatching(string fragment)
    {
        lock (_gate)
        {
            return _statements.Count(
                statement => statement.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) =>
        Record(command, base.ReaderExecuting(command, eventData, result));

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default) =>
        new(Record(command, result));

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result) =>
        Record(command, base.ScalarExecuting(command, eventData, result));

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default) =>
        new(Record(command, result));

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result) =>
        Record(command, base.NonQueryExecuting(command, eventData, result));

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default) =>
        new(Record(command, result));

    private T Record<T>(DbCommand command, T result)
    {
        lock (_gate)
        {
            _statements.Add(command.CommandText);
        }

        return result;
    }

    private static string Flatten(string statement)
    {
        var single = statement.ReplaceLineEndings(" ");

        return single.Length <= 200 ? single : single[..200] + "…";
    }
}
