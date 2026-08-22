using Diaspora.Modules.Community.Domain.Members;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Community.Persistence.Repositories;

/// <summary>EF Core repository for Community's member projection.</summary>
public sealed class CommunityMemberDirectory : ICommunityMemberDirectory
{
    private readonly DiasporaDbContext _dbContext;

    public CommunityMemberDirectory(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CommunityMember> Members => _dbContext.Set<CommunityMember>();

    public async Task<CommunityMember?> FindAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await Members.FirstOrDefaultAsync(
            member => member.UserId == userId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, CommunityMember>> FindManyAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, CommunityMember>();
        }

        var ids = userIds.Where(id => id != Guid.Empty).Select(UserId.From).ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<Guid, CommunityMember>();
        }

        var rows = await Members
            .Where(member => ids.Contains(member.UserId))
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(member => member.UserId.Value);
    }

    public void Add(CommunityMember member) => Members.Add(member);

    public void Update(CommunityMember member) => Members.Update(member);
}
