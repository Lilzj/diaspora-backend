using Diaspora.Platform.Services.Abstractions;
using Diaspora.Platform.Usage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace Diaspora.Persistence.Usage;

/// <summary>
/// The allowance meter, as one guarded upsert.
/// <para>
/// <c>INSERT … ON CONFLICT (id) DO UPDATE SET used = used + 1 WHERE used &lt; @allowance</c>. The
/// first call of a month inserts a row at 1; each later call increments it only while it is under
/// the allowance; the call that finds it full updates nothing and affects zero rows. Checking and
/// taking are the same statement, so there is no gap between them for another machine to use.
/// PostgreSQL and SQLite (3.24+) both accept the syntax, which is what lets the integration suite
/// exercise the same statement production runs.
/// </para>
/// <para>
/// Runs on its own statement, outside any unit of work: a unit taken is taken even if the caller's
/// request later fails, which is the rule <see cref="IProviderUsageMeter"/> states.
/// </para>
/// </summary>
public sealed class EfProviderUsageMeter : IProviderUsageMeter
{
    private static string? _upsertSql;

    private readonly DiasporaDbContext _context;
    private readonly IClock _clock;

    public EfProviderUsageMeter(DiasporaDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<bool> TryConsumeAsync(
        string provider,
        string sku,
        int monthlyAllowance,
        string billingTimeZoneId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        // Without this, an allowance of zero would still insert the month's first row at 1.
        if (monthlyAllowance <= 0) return false;

        var period = ProviderUsageCounter.PeriodOf(_clock.UtcNow, billingTimeZoneId);
        var id = ProviderUsageCounter.IdOf(provider, sku, period);

        _upsertSql ??= BuildUpsertSql();

        // Not user input: every identifier is model metadata run through the provider's delimiter,
        // and every value is bound as a parameter.
#pragma warning disable CA2100
        var affected = await _context.Database.ExecuteSqlRawAsync(
            _upsertSql,
            [id, provider, sku, period, monthlyAllowance],
            cancellationToken);
#pragma warning restore CA2100

        return affected > 0;
    }

    public async Task<int> GetUsedAsync(
        string provider,
        string sku,
        string billingTimeZoneId,
        CancellationToken cancellationToken = default)
    {
        var period = ProviderUsageCounter.PeriodOf(_clock.UtcNow, billingTimeZoneId);
        var id = ProviderUsageCounter.IdOf(provider, sku, period);

        return await _context.Set<ProviderUsageCounter>()
            .AsNoTracking()
            .Where(counter => counter.Id == id)
            .Select(counter => counter.Used)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private string BuildUpsertSql()
    {
        var sql = _context.GetService<ISqlGenerationHelper>();
        var entityType = _context.Model.FindEntityType(typeof(ProviderUsageCounter))
            ?? throw new InvalidOperationException("The provider usage counter is not mapped.");

        var tableName = entityType.GetTableName()!;
        var schema = entityType.GetSchema();
        var table = StoreObjectIdentifier.Table(tableName, schema);

        string Column(string property) =>
            sql.DelimitIdentifier(entityType.FindProperty(property)!.GetColumnName(table)!);

        var qualified = sql.DelimitIdentifier(tableName, schema);
        var id = Column(nameof(ProviderUsageCounter.Id));
        var used = Column(nameof(ProviderUsageCounter.Used));

        return $"INSERT INTO {qualified} " +
               $"({id}, {Column(nameof(ProviderUsageCounter.Provider))}, " +
               $"{Column(nameof(ProviderUsageCounter.Sku))}, " +
               $"{Column(nameof(ProviderUsageCounter.Period))}, {used}) " +
               "VALUES ({0}, {1}, {2}, {3}, 1) " +
               $"ON CONFLICT ({id}) DO UPDATE SET {used} = {qualified}.{used} + 1 " +
               $"WHERE {qualified}.{used} < {{4}}";
    }
}
