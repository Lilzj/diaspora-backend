using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Offers;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Jobs.Persistence.Repositories;

/// <summary>
/// EF Core repository for offers.
/// <para>
/// "Live" throughout means <see cref="OfferStatus.Extended"/> — with the candidate and not yet
/// answered. Whether it has since lapsed is the aggregate's own judgement against the clock, so
/// an offer past its deadline is still returned here and reads as expired to the caller. The
/// alternative — filtering by deadline in SQL — would hide an expired offer from the very sweep
/// whose job is to write down that it expired.
/// </para>
/// </summary>
public sealed class JobOfferRepository : IJobOfferRepository
{
    private readonly DiasporaDbContext _dbContext;

    public JobOfferRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<JobOffer> Offers => _dbContext.Set<JobOffer>();

    public async Task<JobOffer?> GetAsync(
        JobOfferId id, CancellationToken cancellationToken = default) =>
        await Offers.FirstOrDefaultAsync(offer => offer.Id == id, cancellationToken);

    public async Task<JobOffer?> GetForReadAsync(
        JobOfferId id, CancellationToken cancellationToken = default) =>
        await Offers.AsNoTracking()
            .FirstOrDefaultAsync(offer => offer.Id == id, cancellationToken);

    public async Task<JobOffer?> GetLiveForApplicationAsync(
        JobApplicationId applicationId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applicationId);

        return await Offers
            .Where(offer =>
                offer.ApplicationId == applicationId
                && (offer.Status == OfferStatus.Draft || offer.Status == OfferStatus.Extended))
            .OrderByDescending(offer => offer.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JobOffer>> GetForApplicationAsync(
        JobApplicationId applicationId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applicationId);

        return await Offers.AsNoTracking()
            .Where(offer => offer.ApplicationId == applicationId)
            .OrderByDescending(offer => offer.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JobOffer>> GetOutstandingForEmployerAsync(
        EmployerProfileId employerId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employerId);

        return await Offers.AsNoTracking()
            .Where(offer =>
                offer.EmployerId == employerId && offer.Status == OfferStatus.Extended)
            .OrderBy(offer => offer.Terms.ExpiresAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JobOffer>> GetOutstandingForCandidateAsync(
        CandidateProfileId candidateId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidateId);

        return await Offers.AsNoTracking()
            .Where(offer =>
                offer.CandidateId == candidateId && offer.Status == OfferStatus.Extended)
            .OrderBy(offer => offer.Terms.ExpiresAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JobOffer>> GetExpiredAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default) =>
        await Offers
            .Where(offer =>
                offer.Status == OfferStatus.Extended && offer.Terms.ExpiresAt <= asOf)
            .OrderBy(offer => offer.Terms.ExpiresAt)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);

    public async Task AddAsync(JobOffer offer, CancellationToken cancellationToken = default) =>
        await Offers.AddAsync(offer, cancellationToken);

    public void Remove(JobOffer offer) => Offers.Remove(offer);
}
