using Diaspora.Modules.Location.Domain.Experience;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Location.Persistence.Repositories;

/// <summary>EF Core repository for declared residence history.</summary>
public sealed class CountryExperienceRepository : ICountryExperienceRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CountryExperienceRepository(DiasporaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private DbSet<CountryExperience> Experiences => _dbContext.Set<CountryExperience>();

    public async Task<CountryExperience?> GetAsync(
        UserId userId, CountryCode countryCode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(countryCode);

        return await Experiences
            .Include(experience => experience.Expertise)
            .FirstOrDefaultAsync(
                experience => experience.UserId == userId && experience.CountryCode == countryCode,
                cancellationToken);
    }

    public async Task<CountryExperience?> GetByIdAsync(
        CountryExperienceId id, CancellationToken cancellationToken = default) =>
        await Experiences
            .Include(experience => experience.Expertise)
            .FirstOrDefaultAsync(experience => experience.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CountryExperience>> ListByUserAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await Experiences
            .AsNoTracking()
            .Include(experience => experience.Expertise)
            .Where(experience => experience.UserId == userId)
            .OrderByDescending(experience => experience.Period.ArrivedOn)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<UserId, IReadOnlyList<CountryExperience>>> ListByUsersAsync(
        IReadOnlyCollection<UserId> userIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        if (userIds.Count == 0)
        {
            return new Dictionary<UserId, IReadOnlyList<CountryExperience>>();
        }

        // One query for the whole page of members. The alternative — a query per row — is the
        // N+1 that every screen showing a list of people would otherwise inflict, and country
        // badges appear on nearly all of them.
        var wanted = userIds.Distinct().ToList();

        var experiences = await Experiences
            .AsNoTracking()
            .Include(experience => experience.Expertise)
            .Where(experience => wanted.Contains(experience.UserId))
            .OrderByDescending(experience => experience.Period.ArrivedOn)
            .ToListAsync(cancellationToken);

        return experiences
            .GroupBy(experience => experience.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<CountryExperience>)group.ToList());
    }

    public async Task AddAsync(
        CountryExperience experience, CancellationToken cancellationToken = default) =>
        await Experiences.AddAsync(experience, cancellationToken);

    public void Remove(CountryExperience experience) => Experiences.Remove(experience);
}
