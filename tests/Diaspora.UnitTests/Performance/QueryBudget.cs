using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Diaspora.UnitTests.Performance;

/// <summary>
/// Counts the commands a piece of code sends to the database.
/// <para>
/// <b>The platform has guardrails over the route table, the permission catalogue, the policy
/// catalogue, the configuration reference and the migration model, and had none over query
/// counts.</b> BARCH-005 says what that costs: a scan for an awaited data call inside a
/// <c>foreach</c> returned 77 candidate sites, two of them on the hottest read paths, and the only
/// thing that would have caught either before a user did is a number that fails a build. This is
/// that number.
/// </para>
/// <para>
/// <b>An interceptor rather than the EF log, because a count has to be exact.</b>
/// <c>Microsoft.EntityFrameworkCore.Database.Command</c> is a logging category whose output depends
/// on the configured level and whose text has to be matched; <see cref="DbCommandInterceptor"/> is
/// the supported seam, sees every execution path — reader, scalar and non-query — and counts the
/// thing itself rather than a message about it.
/// </para>
/// <para>
/// <b>It records the SQL as well as the count.</b> A budget test that fails says "expected 1, found
/// 50", which is true and tells nobody which fifty; the statements are in the failure message so
/// the loop names itself.
/// </para>
/// </summary>
internal sealed class QueryCountingInterceptor : DbCommandInterceptor
{
    private readonly List<string> _statements = [];

    public IReadOnlyList<string> Statements => _statements;

    public int Count => _statements.Count;

    public void Reset() => _statements.Clear();

    /// <summary>The statements, numbered, for a failure message somebody can act on.</summary>
    public string Describe() =>
        _statements.Count == 0
            ? "(no statements)"
            : string.Join(
                "\n",
                _statements.Select((statement, index) =>
                    $"  {index + 1}. {Flatten(statement)}"));

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result) =>
        Record(command, base.ReaderExecuting(command, eventData, result));

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default) =>
        new(Record(command, result));

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result) =>
        Record(command, base.ScalarExecuting(command, eventData, result));

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default) =>
        new(Record(command, result));

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result) =>
        Record(command, base.NonQueryExecuting(command, eventData, result));

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default) =>
        new(Record(command, result));

    private T Record<T>(DbCommand command, T result)
    {
        _statements.Add(command.CommandText);

        return result;
    }

    private static string Flatten(string statement)
    {
        var single = statement.ReplaceLineEndings(" ");

        return single.Length <= 160 ? single : single[..160] + "…";
    }
}

/// <summary>
/// One model per contributor set, because EF caches a model per <c>DbContext</c> <em>type</em>.
/// <para>
/// Every test host in this suite builds <c>DiasporaDbContext</c> with a different set of
/// contributors, and without a distinct cache key the second host to run silently gets the first
/// one's model — tables missing, and a failure that looks like a bad query rather than a shared
/// cache.
/// </para>
/// </summary>
internal sealed class QueryBudgetModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) =>
        (typeof(QueryBudgetModelCacheKeyFactory), designTime);
}
