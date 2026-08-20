using Diaspora.Modules.Providers.Domain.Categories;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Application.Services;

/// <summary>
/// Resolves category references, and caches the catalogue for the life of one request.
/// <para>
/// <b>The cache is the point.</b> The catalogue is a few hundred rows, read on nearly every
/// request, and changed a handful of times a year — and a discovery page needs a category name
/// for every card. Without a per-request cache that is twenty lookups to render twenty
/// subtitles; with one it is a single query however many cards there are.
/// </para>
/// <para>
/// <b>Scoped, not singleton</b>, deliberately. A singleton would be faster still and would go
/// stale the moment staff renamed a category in production, with no way to tell which instance
/// was serving the old name. B6 wrote the same note over its reaction catalogue; paying one
/// query per request is a small price for a catalogue that is never wrong.
/// </para>
/// <para>
/// <see cref="ResolveAsync"/> accepts an identifier, a slug, a name or a registered alias, which
/// is what makes the frontend's three disjoint category vocabularies merge without a client
/// release. A request still sending <c>Restaurants</c> lands on the <c>restaurants</c> node.
/// </para>
/// </summary>
public sealed class ServiceCategoryResolver
{
    private readonly IServiceCategoryRepository _repository;

    private IReadOnlyList<ServiceCategory>? _all;
    private Dictionary<Guid, ServiceCategory>? _byId;

    public ServiceCategoryResolver(IServiceCategoryRepository repository) =>
        _repository = repository;

    /// <summary>The whole active tree, ordered by path. Cached for this request.</summary>
    public async Task<IReadOnlyList<ServiceCategory>> ListAsync(
        CancellationToken cancellationToken = default) =>
        _all ??= await _repository.ListAllAsync(includeRetired: false, cancellationToken);

    /// <summary>
    /// Every category needed to render a set of providers, keyed by identifier.
    /// <para>
    /// Backed by the cached tree rather than by a targeted query, because the tree is already in
    /// hand for any request that has rendered one card, and a second query for a subset of what
    /// is already loaded is strictly worse. Retired categories are fetched individually, because
    /// a provider still filed under one must still render a name.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, ServiceCategory>> LookupAsync(
        IReadOnlyCollection<ServiceCategoryId> categoryIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(categoryIds);

        if (categoryIds.Count == 0)
        {
            return new Dictionary<Guid, ServiceCategory>();
        }

        await EnsureIndexAsync(cancellationToken);

        var found = new Dictionary<Guid, ServiceCategory>(categoryIds.Count);
        var missing = new List<ServiceCategoryId>();

        foreach (var categoryId in categoryIds.Distinct())
        {
            if (_byId!.TryGetValue(categoryId.Value, out var category))
            {
                found[categoryId.Value] = category;
            }
            else
            {
                missing.Add(categoryId);
            }
        }

        if (missing.Count == 0)
        {
            return found;
        }

        foreach (var category in await _repository.GetManyAsync(missing, cancellationToken))
        {
            found[category.Id.Value] = category;
            _byId![category.Id.Value] = category;
        }

        return found;
    }

    /// <summary>One category by identifier, from the cache where possible.</summary>
    public async Task<ServiceCategory?> FindAsync(
        ServiceCategoryId categoryId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(categoryId);

        await EnsureIndexAsync(cancellationToken);

        if (_byId!.TryGetValue(categoryId.Value, out var cached))
        {
            return cached;
        }

        var category = await _repository.GetAsync(categoryId, cancellationToken);

        if (category is not null)
        {
            _byId[category.Id.Value] = category;
        }

        return category;
    }

    /// <summary>
    /// Resolves whatever a caller sent — an identifier, a slug, a name or a legacy alias.
    /// <para>
    /// The identifier case is checked first and cheaply, because that is what a well-behaved
    /// client sends after its first bootstrap. Everything else falls through to the catalogue's
    /// own resolution, which is where aliases live.
    /// </para>
    /// </summary>
    public async Task<Result<ServiceCategory>> ResolveAsync(
        string? term, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return Result.Failure<ServiceCategory>(ProviderErrors.CategoryNotFound);
        }

        if (Guid.TryParse(term, out var identifier))
        {
            var byId = await FindAsync(ServiceCategoryId.From(identifier), cancellationToken);

            if (byId is not null)
            {
                return Result.Success(byId);
            }
        }

        await EnsureIndexAsync(cancellationToken);

        var cached = _all!.FirstOrDefault(category => category.Matches(term));

        if (cached is not null)
        {
            return Result.Success(cached);
        }

        var resolved = await _repository.ResolveAsync(term, cancellationToken);

        return resolved is null
            ? Result.Failure<ServiceCategory>(ProviderErrors.CategoryNotFound)
            : Result.Success(resolved);
    }

    /// <summary>
    /// Resolves a category and refuses a retired one.
    /// <para>
    /// Used on the write path only. A read must keep resolving retired categories — providers
    /// filed under one still need a name on their card — but nobody new may choose it, and
    /// failing at the point of choice is how a provider finds that out.
    /// </para>
    /// </summary>
    public async Task<Result<ServiceCategory>> ResolveForWriteAsync(
        string? term, CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAsync(term, cancellationToken);

        if (resolved.IsFailure)
        {
            return resolved;
        }

        return resolved.Value.IsActive
            ? resolved
            : Result.Failure<ServiceCategory>(ProviderErrors.CategoryInactive);
    }

    private async Task EnsureIndexAsync(CancellationToken cancellationToken)
    {
        _all ??= await _repository.ListAllAsync(includeRetired: false, cancellationToken);

        _byId ??= _all.ToDictionary(category => category.Id.Value);
    }
}
