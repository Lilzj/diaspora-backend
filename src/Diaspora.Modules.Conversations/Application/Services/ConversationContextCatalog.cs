using Diaspora.Modules.Conversations.Domain.Catalog;
using Diaspora.Modules.Conversations.Domain.Errors;
using Diaspora.Modules.Conversations.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Conversations.Application.Services;

/// <summary>
/// The one place a caller-supplied context type is checked against the catalogue.
/// <para>
/// <b>Checked here rather than inside <see cref="ConversationType"/>, because a value object that
/// needed a repository could not be constructed in a unit test.</b> The type is validated once, at
/// the only place a caller supplies one, and everything downstream carries a value object that is
/// known to name a real row.
/// </para>
/// <para>
/// Cached per request rather than per process. The catalogue changes only when a seeder runs at
/// startup, so a longer cache would be safe — but this module has no cache service to depend on
/// (the kernel declares <c>ICacheService</c> and nothing implements it), and a scoped dictionary
/// collapses the repeated lookups inside one inbox render, which is where they actually happen.
/// </para>
/// </summary>
public sealed class ConversationContextCatalog
{
    private readonly IConversationContextTypeRepository _types;
    private readonly Dictionary<string, ConversationContextType?> _cache =
        new(StringComparer.Ordinal);

    public ConversationContextCatalog(IConversationContextTypeRepository types) => _types = types;

    /// <summary>The catalogue row for a key, or null when the key names nothing active.</summary>
    public async Task<ConversationContextType?> FindAsync(
        string? key, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var trimmed = key.Trim();

        if (_cache.TryGetValue(trimmed, out var cached))
        {
            return cached;
        }

        var row = await _types.GetAsync(trimmed, cancellationToken);

        // A deactivated type is not a match. Retiring a vertical must stop new conversations while
        // leaving the ones that exist readable — which is why deactivation, not deletion.
        var resolved = row is { IsActive: true } ? row : null;

        _cache[trimmed] = resolved;

        return resolved;
    }

    /// <summary>
    /// Validates a key and returns the value object, or the catalogue failure.
    /// </summary>
    public async Task<Result<ConversationType>> ResolveAsync(
        string? key, CancellationToken cancellationToken)
    {
        var row = await FindAsync(key, cancellationToken);

        return row is null
            ? Result.Failure<ConversationType>(ConversationErrors.UnknownContextType)
            : Result.Success(ConversationType.From(row.Key));
    }

    /// <summary>Every active type, for the client's filter chips.</summary>
    public Task<IReadOnlyList<ConversationContextType>> ListAsync(
        CancellationToken cancellationToken) =>
        _types.ListAsync(activeOnly: true, cancellationToken);
}
