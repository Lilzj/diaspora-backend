using Diaspora.Modules.Community.Domain.Reactions;
using Diaspora.Modules.Community.Extensibility.Reactions;

namespace Diaspora.Modules.Community.Application.Services;

/// <summary>
/// Fills the in-memory reaction catalogue from the database, once per request.
/// <para>
/// Kept out of the catalogue itself so the catalogue stays a plain in-memory type with no
/// repository dependency — which is what lets a unit test build one from five definitions
/// without a database.
/// </para>
/// </summary>
public sealed class ReactionCatalogLoader
{
    private readonly IReactionDefinitionRepository _definitions;
    private readonly ReactionCatalog _catalog;

    public ReactionCatalogLoader(
        IReactionDefinitionRepository definitions, ReactionCatalog catalog)
    {
        _definitions = definitions;
        _catalog = catalog;
    }

    public async Task<ReactionCatalog> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_catalog.IsLoaded)
        {
            return _catalog;
        }

        _catalog.Load(await _definitions.ListAsync(false, cancellationToken));

        return _catalog;
    }
}
