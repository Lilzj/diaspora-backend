using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Votes;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Persistence.Repositories;

/// <summary>
/// EF Core repository for helpfulness votes.
/// <para>
/// The (guide, voter) lookup goes through the denormalised <c>VoterKey</c> rather than the pair
/// of identifier columns, because the key carries the unique index — the same derivation the
/// aggregate performs, so the two can never disagree.
/// </para>
/// </summary>
public sealed class GuideVoteRepository : IGuideVoteRepository
{
    private readonly DiasporaDbContext _dbContext;

    public GuideVoteRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<GuideVote> Votes => _dbContext.Set<GuideVote>();

    public async Task<GuideVote?> GetAsync(
        GuideVoteId id, CancellationToken cancellationToken = default) =>
        await Votes.FirstOrDefaultAsync(vote => vote.Id == id, cancellationToken);

    public async Task<GuideVote?> GetByGuideAndVoterAsync(
        GuideId guideId, UserId voterId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(guideId);
        ArgumentNullException.ThrowIfNull(voterId);

        // The same derivation GuideVote.KeyFor performs. Queried instead of the identifier
        // pair so the lookup rides the unique index.
        var key = $"{guideId.Value:N}:{voterId.Value:N}";

        return await Votes.FirstOrDefaultAsync(vote => vote.VoterKey == key, cancellationToken);
    }

    public async Task AddAsync(GuideVote vote, CancellationToken cancellationToken = default) =>
        await Votes.AddAsync(vote, cancellationToken);

    public void Remove(GuideVote vote) => Votes.Remove(vote);
}
