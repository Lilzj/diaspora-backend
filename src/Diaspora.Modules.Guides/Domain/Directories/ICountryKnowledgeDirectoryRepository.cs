using Diaspora.Modules.Guides.Domain.Identifiers;

namespace Diaspora.Modules.Guides.Domain.Directories;

/// <summary>
/// Reads and writes country knowledge directories.
/// <para>
/// <b>Two read shapes, and the split matters more here than almost anywhere else in the
/// platform.</b> A country page is the most-read screen this module serves and it needs the whole
/// graph — entries, featured guides, translations. The country <em>picker</em> needs a name and a
/// flag for every country the platform serves, and loading the full graph for a hundred and
/// ninety-five of them to render a dropdown is several thousand rows nobody looks at.
/// </para>
/// </summary>
public interface ICountryKnowledgeDirectoryRepository
{
    /// <summary>The full graph, tracked. For commands.</summary>
    Task<CountryKnowledgeDirectory?> GetAsync(
        string countryCode, CancellationToken cancellationToken = default);

    /// <summary>The full graph, untracked. For the country page.</summary>
    Task<CountryKnowledgeDirectory?> GetForReadAsync(
        string countryCode, CancellationToken cancellationToken = default);

    Task<CountryKnowledgeDirectory?> GetByIdAsync(
        CountryKnowledgeDirectoryId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The full graph including an archived directory, tracked.
    /// <para>
    /// The undelete blind spot, closed deliberately: archiving is a soft delete, the global filter
    /// hides the row, and a restore has to load the very row that was hidden. This is the only
    /// member that ignores the filter for a whole aggregate.
    /// </para>
    /// </summary>
    Task<CountryKnowledgeDirectory?> GetIncludingArchivedAsync(
        string countryCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every directory, headers only — no entries, no featured guides, no translations.
    /// <para>
    /// What the country picker and the "browse by country" grid render. Deliberately not paged:
    /// there are fewer than two hundred countries, the answer fits in one response, and a cursor
    /// over a list that short is machinery nobody needs.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<CountryKnowledgeDirectory>> ListHeadersAsync(
        bool publishedOnly = true, CancellationToken cancellationToken = default);

    /// <summary>Whether a country already has a directory, archived ones included.</summary>
    Task<bool> ExistsAsync(string countryCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Which published directories still list a category. The guard behind archiving one.
    /// <para>
    /// Answered as a list of country codes rather than a count, because the curator being stopped
    /// needs to know <em>where</em> to go and remove it, and a bare number sends them hunting
    /// through every country.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<string>> CountriesListingCategoryAsync(
        string knowledgeCategoryKey, CancellationToken cancellationToken = default);

    Task AddAsync(
        CountryKnowledgeDirectory directory, CancellationToken cancellationToken = default);

    /// <summary>Archives, through the platform's soft-delete path. Never a hard delete.</summary>
    void Remove(CountryKnowledgeDirectory directory);
}
