using Diaspora.Contracts.Guides.V1.Authors;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Guides.Persistence;

/// <summary>
/// Guides' answer to <see cref="IAuthorGuideStatsReader"/>: one aggregate over the author's
/// published guides' engagement counters — the same counters each guide's own page serves.
/// <para>
/// Soft-deleted rows are excluded by the context's global filter.
/// </para>
/// </summary>
public sealed class AuthorGuideStatsReader : IAuthorGuideStatsReader
{
    private readonly DiasporaDbContext _dbContext;

    public AuthorGuideStatsReader(DiasporaDbContext dbContext) => _dbContext = dbContext;

    public async Task<AuthorGuideStats> GetAsync(
        Guid authorUserId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(authorUserId, Guid.Empty);

        var authorId = UserId.From(authorUserId);

        var totals = await _dbContext.Set<Guide>()
            .AsNoTracking()
            .Where(guide => guide.AuthorId == authorId && guide.Status == GuideStatus.Published)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Guides = group.Count(),
                Views = group.Sum(guide => guide.Engagement.Views),
                UniqueViews = group.Sum(guide => guide.Engagement.UniqueViews),
                Helpful = group.Sum(guide => (long)guide.Engagement.HelpfulCount),
                NotHelpful = group.Sum(guide => (long)guide.Engagement.NotHelpfulCount),
                Bookmarks = group.Sum(guide => (long)guide.Engagement.BookmarkCount)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return totals is null
            ? new AuthorGuideStats
            {
                PublishedGuides = 0,
                Views = 0,
                UniqueViews = 0,
                HelpfulVotes = 0,
                NotHelpfulVotes = 0,
                Bookmarks = 0
            }
            : new AuthorGuideStats
            {
                PublishedGuides = totals.Guides,
                Views = totals.Views,
                UniqueViews = totals.UniqueViews,
                HelpfulVotes = totals.Helpful,
                NotHelpfulVotes = totals.NotHelpful,
                Bookmarks = totals.Bookmarks
            };
    }
}
