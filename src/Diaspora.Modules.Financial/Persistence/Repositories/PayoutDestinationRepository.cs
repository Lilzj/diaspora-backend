using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Withdrawals;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>EF Core repository for payout destinations.</summary>
public sealed class PayoutDestinationRepository : IPayoutDestinationRepository
{
    private readonly DiasporaDbContext _dbContext;

    public PayoutDestinationRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<PayoutDestination> Destinations => _dbContext.Set<PayoutDestination>();

    public void Add(PayoutDestination destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        Destinations.Add(destination);
    }

    public void Update(PayoutDestination destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        FinancialAggregateUpdater.MarkDirty(_dbContext, destination);
    }

    public async Task<PayoutDestination?> FindAsync(
        PayoutDestinationId destinationId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destinationId);

        return await Destinations.FirstOrDefaultAsync(
            destination => destination.Id == destinationId, cancellationToken);
    }

    public async Task<IReadOnlyList<PayoutDestination>> ListForUserAsync(
        Guid userId, bool includeRemoved, CancellationToken cancellationToken = default)
    {
        var destinations = Destinations.Where(destination => destination.OwnerUserId == userId);

        if (!includeRemoved)
        {
            destinations = destinations.Where(destination => !destination.IsRemoved);
        }

        return await destinations
            .OrderByDescending(destination => destination.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
