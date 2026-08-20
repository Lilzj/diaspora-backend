using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;

namespace Diaspora.Modules.Community.Domain.Hashtags;

/// <summary>Reads and writes hashtags.</summary>
public interface IHashtagRepository
{
    Task<Hashtag?> FindAsync(string slug, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Hashtag>> FindManyAsync(
        IReadOnlyCollection<string> slugs, CancellationToken cancellationToken = default);

    /// <summary>Prefix search for the composer's autocomplete. Suppressed tags never appear.</summary>
    Task<IReadOnlyList<Hashtag>> SuggestAsync(
        string prefix, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// The trending list, ordered by the stored window rather than computed live. Optionally
    /// narrowed to one country, which is what makes a hyperlocal trend visible at all next to
    /// platform-wide noise.
    /// </summary>
    Task<IReadOnlyList<Hashtag>> ListTrendingAsync(
        string? countryCode, int limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Hashtag>> ListForWindowRollAsync(
        int batchSize, CancellationToken cancellationToken = default);

    Task AddAsync(Hashtag hashtag, CancellationToken cancellationToken = default);
}
