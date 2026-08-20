using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Domain.KnowledgeAnalytics;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Guides.Persistence.Repositories;

/// <summary>
/// EF Core repository for the per-country search phrase counters.
/// <para>
/// A country of null or blank normalizes to the empty string, matching what the aggregate stores.
/// The two must agree: a lookup that searched for null while the write stored an empty string would
/// create a new row on every global search and turn the counter table into a log.
/// </para>
/// </summary>
public sealed class KnowledgeSearchTermRepository : IKnowledgeSearchTermRepository
{
    private readonly DiasporaDbContext _dbContext;

    public KnowledgeSearchTermRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<KnowledgeSearchTerm> Terms => _dbContext.Set<KnowledgeSearchTerm>();

    public async Task<KnowledgeSearchTerm?> GetAsync(
        string countryCode, string normalizedTerm, CancellationToken cancellationToken = default)
    {
        var country = Country(countryCode);

        return string.IsNullOrEmpty(normalizedTerm)
            ? null
            : await Terms.FirstOrDefaultAsync(
                term => term.CountryCode == country && term.Term == normalizedTerm,
                cancellationToken);
    }

    public async Task<IReadOnlyList<KnowledgeSearchTerm>> TopAsync(
        string countryCode,
        int limit,
        DateTimeOffset? since = null,
        CancellationToken cancellationToken = default)
    {
        var country = Country(countryCode);

        var query = Terms.AsNoTracking().Where(term => term.CountryCode == country);

        if (since is { } window)
        {
            query = query.Where(term => term.LastSearchedAt >= window);
        }

        return await query
            .OrderByDescending(term => term.SearchCount)
            .ThenByDescending(term => term.LastSearchedAt)
            .ThenBy(term => term.Term)
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Phrases that currently find nothing, most searched first.
    /// <para>
    /// The commissioning queue, and the most directly actionable read in the phase: every row is a
    /// guide somebody wanted and nobody has written, named in the words they used.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<KnowledgeSearchTerm>> UnansweredAsync(
        string countryCode, int limit, CancellationToken cancellationToken = default)
    {
        var country = Country(countryCode);

        return await Terms
            .AsNoTracking()
            .Where(term => term.CountryCode == country && term.ResultCount == 0)
            .OrderByDescending(term => term.SearchCount)
            .ThenByDescending(term => term.LastSearchedAt)
            .ThenBy(term => term.Term)
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        KnowledgeSearchTerm term, CancellationToken cancellationToken = default) =>
        await Terms.AddAsync(term, cancellationToken);

    private static string Country(string? countryCode) =>
        KnowledgeNormalization.Country(countryCode) ?? string.Empty;
}
