using Diaspora.Modules.Conversations.Domain.Catalog;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Conversations.Persistence.Repositories;

/// <summary>The context type catalogue.</summary>
public sealed class ConversationContextTypeRepository : IConversationContextTypeRepository
{
    private readonly DiasporaDbContext _dbContext;

    public ConversationContextTypeRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<ConversationContextType> Types => _dbContext.Set<ConversationContextType>();

    public async Task<ConversationContextType?> GetAsync(
        string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return await Types.FirstOrDefaultAsync(type => type.Id == key, cancellationToken);
    }

    public async Task<IReadOnlyList<ConversationContextType>> ListAsync(
        bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var query = Types.AsQueryable();

        if (activeOnly)
        {
            query = query.Where(type => type.IsActive);
        }

        return await query
            .OrderBy(type => type.Position)
            .ThenBy(type => type.Id)
            .ToListAsync(cancellationToken);
    }

    public void Add(ConversationContextType contextType)
    {
        ArgumentNullException.ThrowIfNull(contextType);

        Types.Add(contextType);
    }

    public void Update(ConversationContextType contextType)
    {
        ArgumentNullException.ThrowIfNull(contextType);

        if (_dbContext.Entry(contextType).State == EntityState.Detached)
        {
            Types.Update(contextType);
        }
    }
}
