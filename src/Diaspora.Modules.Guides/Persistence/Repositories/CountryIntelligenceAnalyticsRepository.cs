using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Domain.IntelligenceAnalytics;
using Diaspora.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Guides.Persistence.Repositories;

/// <summary>
/// EF Core repository for per-country intelligence usage counters.
/// <para>
/// <see cref="GetOrStartAsync"/> adds the new row to the change tracker and does not save. The
/// caller's unit of work does that, so a request recording three signals costs one insert rather
/// than three — and, more importantly, a recorder on a read path never commits on its own, which is
/// what would make an analytics failure able to roll back somebody's page.
/// </para>
/// </summary>
public sealed class CountryIntelligenceAnalyticsRepository
    : ICountryIntelligenceAnalyticsRepository
{
    private readonly DiasporaDbContext _dbContext;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CountryIntelligenceAnalyticsRepository(
        DiasporaDbContext dbContext, IGuidGenerator guids, IClock clock)
    {
        _dbContext = dbContext;
        _guids = guids;
        _clock = clock;
    }

    private DbSet<CountryIntelligenceAnalytics> Analytics =>
        _dbContext.Set<CountryIntelligenceAnalytics>();

    public async Task<CountryIntelligenceAnalytics?> GetForCountryAsync(
        string countryCode, CancellationToken cancellationToken = default)
    {
        var country = IntelligenceNormalization.Country(countryCode);

        return country is null
            ? null
            : await Analytics
                .Include(analytics => analytics.Counters)
                .FirstOrDefaultAsync(
                    analytics => analytics.CountryCode == country, cancellationToken);
    }

    public async Task<CountryIntelligenceAnalytics> GetOrStartAsync(
        string countryCode, CancellationToken cancellationToken = default)
    {
        var existing = await GetForCountryAsync(countryCode, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var started = CountryIntelligenceAnalytics.Start(
            CountryIntelligenceAnalyticsId.From(_guids.NewSequentialGuid()),
            countryCode,
            _clock.UtcNow);

        await Analytics.AddAsync(started, cancellationToken);

        return started;
    }

    public async Task<IReadOnlyList<CountryIntelligenceAnalytics>> ListAsync(
        CancellationToken cancellationToken = default) =>
        await Analytics
            .AsNoTracking()
            .Include(analytics => analytics.Counters)
            .OrderBy(analytics => analytics.CountryCode)
            .ToListAsync(cancellationToken);
}
