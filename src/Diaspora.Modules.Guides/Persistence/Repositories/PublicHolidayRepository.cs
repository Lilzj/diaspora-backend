using Diaspora.Modules.Guides.Domain.Holidays;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Guides.Persistence.Repositories;

/// <summary>
/// EF Core repository for the public holiday calendar.
/// <para>
/// No paging, deliberately: a country has ten to thirty holidays in a year, and both real queries
/// return a set small enough to hand over whole. See the interface for why a cursor here would be
/// machinery nobody needs.
/// </para>
/// <para>
/// <b>The range query overlaps rather than contains.</b> A multi-day observance that started before
/// the window and ends inside it is very much part of "what is coming up" — somebody planning an
/// appointment on the second day of a three-day closure needs to be told. Matching on the start date
/// alone would silently drop exactly the entries a member is most likely to trip over.
/// </para>
/// </summary>
public sealed class PublicHolidayRepository : IPublicHolidayRepository
{
    private readonly DiasporaDbContext _dbContext;

    public PublicHolidayRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<PublicHoliday> Holidays => _dbContext.Set<PublicHoliday>();

    public async Task<PublicHoliday?> GetAsync(
        PublicHolidayId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Holidays)
            .FirstOrDefaultAsync(holiday => holiday.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<PublicHoliday?> GetIncludingArchivedAsync(
        PublicHolidayId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Holidays.IgnoreQueryFilters())
            .FirstOrDefaultAsync(holiday => holiday.Id == id, cancellationToken);

    public async Task<IReadOnlyList<PublicHoliday>> ListForYearAsync(
        string countryCode, int year, CancellationToken cancellationToken = default)
    {
        var country = IntelligenceNormalization.Country(countryCode);

        return country is null
            ? []
            : await FullGraph(Holidays.AsNoTracking())
                .Where(holiday => holiday.CountryCode == country && holiday.Year == year)
                .OrderBy(holiday => holiday.ObservedOn)
                .ThenBy(holiday => holiday.Name)
                .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PublicHoliday>> ListBetweenAsync(
        string countryCode,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken = default)
    {
        var country = IntelligenceNormalization.Country(countryCode);

        return country is null
            ? []
            : await FullGraph(Holidays.AsNoTracking())
                .Where(holiday =>
                    holiday.CountryCode == country
                    && holiday.ObservedOn <= until
                    && holiday.ObservedUntil >= from)
                .OrderBy(holiday => holiday.ObservedOn)
                .ThenBy(holiday => holiday.Name)
                .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        PublicHoliday holiday, CancellationToken cancellationToken = default) =>
        await Holidays.AddAsync(holiday, cancellationToken);

    /// <inheritdoc cref="Domain.Intelligence.IOfficialNoticeRepository.Remove" />
    public void Remove(PublicHoliday holiday) => Holidays.Remove(holiday);

    private static IQueryable<PublicHoliday> FullGraph(IQueryable<PublicHoliday> query) =>
        query.Include(holiday => holiday.Localizations);
}
