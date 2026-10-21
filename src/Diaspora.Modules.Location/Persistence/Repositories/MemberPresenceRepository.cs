using Diaspora.Modules.Location.Domain.Presence;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Location.Persistence.Repositories;

/// <summary>EF Core repository for the member's presence setting and last area.</summary>
public sealed class MemberPresenceRepository : IMemberPresenceRepository
{
    private readonly DiasporaDbContext _dbContext;

    public MemberPresenceRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    public Task<MemberPresence?> FindAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return _dbContext.Set<MemberPresence>()
            .FirstOrDefaultAsync(presence => presence.UserId == userId, cancellationToken);
    }

    public void Add(MemberPresence presence)
    {
        ArgumentNullException.ThrowIfNull(presence);
        _dbContext.Set<MemberPresence>().Add(presence);
    }
}
