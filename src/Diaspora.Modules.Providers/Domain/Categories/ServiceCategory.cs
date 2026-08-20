using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Events;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Domain.Categories;

/// <summary>
/// One node in the service category catalogue.
/// <para>
/// <b>An aggregate rather than an enum, and that is the brief's requirement made concrete:</b>
/// "Categories must be extensible." An enum needs a release to add a category; a catalogue needs
/// a row. The frontend proves why it matters — it carries three disjoint category vocabularies
/// (ten <c>mockCategories</c>, eighteen <c>OrgCategory</c> members, four free-text creator
/// strings) that overlap and contradict, and one of them contains a member commented
/// <c>// future-ready</c> because somebody knew the collision was coming.
/// </para>
/// <para>
/// <b>Hierarchical, with a materialised <see cref="CategoryPath"/>.</b> Two levels of real depth
/// — Food &amp; Restaurants → Restaurants, Finance &amp; Legal → Immigration Consultants — plus
/// headroom for a third. Deeper than that and a member browsing on a phone is three taps from
/// anything, which is why <see cref="ProviderLimits.MaxCategoryDepth"/> is a hard stop and not a
/// guideline.
/// </para>
/// <para>
/// <b>Retired, never deleted.</b> A category with providers under it cannot be removed without
/// either orphaning them or silently recategorising them, and both are worse than an inactive
/// row that stops being offered in the filter. <see cref="Retire"/> is the only way out.
/// </para>
/// </summary>
public sealed class ServiceCategory : AggregateRoot<ServiceCategoryId>, IAuditableEntity
{
    public const int MaxNameLength = 160;

    public const int MaxDescriptionLength = 1_000;

    public const int MaxIconLength = 64;

    private readonly List<ServiceCategoryAlias> _aliases = [];
    private readonly List<ServiceCategoryTranslation> _translations = [];
    private readonly List<ServiceCategoryKeyword> _keywords = [];

    private ServiceCategory(
        ServiceCategoryId id,
        CategorySlug slug,
        string name,
        CategoryPath path,
        ServiceCategoryId? parentId,
        DateTimeOffset createdAt)
        : base(id)
    {
        Slug = slug;
        Name = name;
        Path = path;
        ParentId = parentId;
        CreatedAt = createdAt;
    }

    private ServiceCategory() { }

    /// <summary>The stable key. Immutable — it is what every link and saved filter points at.</summary>
    public CategorySlug Slug { get; private set; } = default!;

    /// <summary>The default-language display name. The client's <c>category.name</c>.</summary>
    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>
    /// The client's <c>category.icon</c> (an emoji in <c>mockCategories</c>) or its
    /// <c>iconName</c> (an Ionicons name in <c>mockListings</c>). Stored as an opaque string
    /// because which of the two a client wants is a client decision, and validating it here
    /// would mean B7 having an opinion about an icon set it has never seen.
    /// </summary>
    public string? Icon { get; private set; }

    public ServiceCategoryId? ParentId { get; private set; }

    /// <summary>Materialised. The subtree query is a prefix match on this.</summary>
    public CategoryPath Path { get; private set; } = default!;

    /// <summary>Where it sits in the filter row. Curated, because alphabetical is rarely right.</summary>
    public int SortOrder { get; private set; }

    /// <summary>False once retired. Retired categories keep resolving and stop being offered.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Denormalised count of active providers at or beneath this node.
    /// <para>
    /// Kept here so the category chip row can render "Food &amp; Restaurants (128)" without a
    /// count query per chip — which for a ten-chip row on the discovery tab is ten aggregate
    /// scans on every page load. Approximate by design and refreshed by a sweeper: a headline
    /// number that is an hour stale is a far better trade than a browse screen that is slow.
    /// </para>
    /// </summary>
    public int ProviderCount { get; private set; }

    public IReadOnlyList<ServiceCategoryAlias> Aliases => _aliases.AsReadOnly();

    public IReadOnlyList<ServiceCategoryTranslation> Translations => _translations.AsReadOnly();

    public IReadOnlyList<ServiceCategoryKeyword> Keywords => _keywords.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    /// <summary>Whether this node has no parent.</summary>
    public bool IsRoot => ParentId is null;

    public static Result<ServiceCategory> CreateRoot(
        ServiceCategoryId id,
        CategorySlug slug,
        string? name,
        DateTimeOffset now,
        string? icon = null,
        string? description = null,
        int sortOrder = 0)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(slug);

        var trimmedName = name?.Trim();

        if (string.IsNullOrEmpty(trimmedName) || trimmedName.Length > MaxNameLength)
        {
            return Result.Failure<ServiceCategory>(ProviderErrors.NameEmpty);
        }

        var category = new ServiceCategory(id, slug, trimmedName, CategoryPath.Root(slug), null, now)
        {
            Icon = Trim(icon, MaxIconLength),
            Description = Trim(description, MaxDescriptionLength),
            SortOrder = sortOrder
        };

        category.RaiseCreated();

        return Result.Success(category);
    }

    public static Result<ServiceCategory> CreateChild(
        ServiceCategoryId id,
        ServiceCategory parent,
        CategorySlug slug,
        string? name,
        DateTimeOffset now,
        string? icon = null,
        string? description = null,
        int sortOrder = 0)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(slug);

        var trimmedName = name?.Trim();

        if (string.IsNullOrEmpty(trimmedName) || trimmedName.Length > MaxNameLength)
        {
            return Result.Failure<ServiceCategory>(ProviderErrors.NameEmpty);
        }

        if (parent.Path.Depth >= ProviderLimits.MaxCategoryDepth)
        {
            return Result.Failure<ServiceCategory>(ProviderErrors.CategoryDepthExceeded);
        }

        var category = new ServiceCategory(
            id, slug, trimmedName, CategoryPath.Below(parent.Path, slug), parent.Id, now)
        {
            Icon = Trim(icon, MaxIconLength) ?? parent.Icon,
            Description = Trim(description, MaxDescriptionLength),
            SortOrder = sortOrder
        };

        category.RaiseCreated();

        return Result.Success(category);
    }

    /// <summary>Renames the category. The slug and the path are untouched.</summary>
    public Result Rename(string? name, string? description, string? icon, int? sortOrder)
    {
        var trimmedName = name?.Trim();

        if (string.IsNullOrEmpty(trimmedName) || trimmedName.Length > MaxNameLength)
        {
            return Result.Failure(ProviderErrors.NameEmpty);
        }

        Name = trimmedName;
        Description = Trim(description, MaxDescriptionLength);
        Icon = Trim(icon, MaxIconLength);

        if (sortOrder is { } order)
        {
            SortOrder = order;
        }

        RaiseDomainEvent(new ServiceCategoryChangedDomainEvent
        {
            CategoryId = Id,
            Slug = Slug.Value,
            Change = "renamed",
            ParentId = ParentId
        });

        return Result.Success();
    }

    /// <summary>
    /// Moves the category beneath a different parent, or to the root when null.
    /// <para>
    /// <b>Every descendant's path has to be rewritten too</b>, and this aggregate cannot see
    /// its descendants — so it returns its new path and the caller (which loaded the subtree)
    /// applies <see cref="Reparent"/> to each. That is the one place the tree is manipulated
    /// outside a single aggregate, and it is why the move command is transactional.
    /// </para>
    /// </summary>
    public Result Move(ServiceCategory? parent)
    {
        if (parent is not null)
        {
            if (parent.Id == Id)
            {
                return Result.Failure(ProviderErrors.CategoryCycle);
            }

            // A node cannot move beneath its own descendant. The path makes the check one
            // comparison rather than a walk, which is the whole reason it is materialised.
            if (parent.Path.IsWithin(Path))
            {
                return Result.Failure(ProviderErrors.CategoryCycle);
            }

            if (parent.Path.Depth >= ProviderLimits.MaxCategoryDepth)
            {
                return Result.Failure(ProviderErrors.CategoryDepthExceeded);
            }
        }

        ParentId = parent?.Id;
        Path = parent is null ? CategoryPath.Root(Slug) : CategoryPath.Below(parent.Path, Slug);

        RaiseDomainEvent(new ServiceCategoryChangedDomainEvent
        {
            CategoryId = Id,
            Slug = Slug.Value,
            Change = "moved",
            ParentId = ParentId
        });

        return Result.Success();
    }

    /// <summary>
    /// Rewrites this node's path after an ancestor moved. Applied to every descendant of a
    /// moved node, in the same transaction as the move.
    /// </summary>
    public void Reparent(CategoryPath parentPath)
    {
        ArgumentNullException.ThrowIfNull(parentPath);

        Path = CategoryPath.Below(parentPath, Slug);
    }

    /// <summary>
    /// Withdraws the category from the filter row without deleting it.
    /// <para>
    /// Providers already under it keep resolving — their category still has a name, an icon and
    /// a path — and nobody new can choose it. Deleting instead would either orphan them or
    /// recategorise them silently, and a restaurant that woke up filed under "Other" would have
    /// no way to know why its traffic fell.
    /// </para>
    /// </summary>
    public void Retire()
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;

        RaiseDomainEvent(new ServiceCategoryChangedDomainEvent
        {
            CategoryId = Id,
            Slug = Slug.Value,
            Change = "retired",
            ParentId = ParentId
        });
    }

    public void Reinstate()
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;

        RaiseDomainEvent(new ServiceCategoryChangedDomainEvent
        {
            CategoryId = Id,
            Slug = Slug.Value,
            Change = "reinstated",
            ParentId = ParentId
        });
    }

    public Result AddAlias(ServiceCategoryAliasId id, string? value, string? source = null)
    {
        if (_aliases.Count >= ProviderLimits.MaxCategoryAliases)
        {
            return Result.Failure(ProviderErrors.CategoryDepthExceeded);
        }

        var alias = ServiceCategoryAlias.Create(id, value, source);

        if (alias is null)
        {
            return Result.Success();
        }

        if (_aliases.Exists(existing =>
                string.Equals(existing.Normalized, alias.Normalized, StringComparison.Ordinal)))
        {
            return Result.Success();
        }

        _aliases.Add(alias);

        return Result.Success();
    }

    public void RemoveAlias(ServiceCategoryAliasId id)
    {
        var alias = _aliases.SingleOrDefault(candidate => candidate.Id == id);

        if (alias is not null)
        {
            _aliases.Remove(alias);
        }
    }

    public void SetTranslation(
        ServiceCategoryTranslationId id, string? languageCode, string? name, string? description = null)
    {
        var translation = ServiceCategoryTranslation.Create(id, languageCode, name, description);

        if (translation is null)
        {
            return;
        }

        var existing = _translations.SingleOrDefault(candidate =>
            string.Equals(candidate.LanguageCode, translation.LanguageCode, StringComparison.Ordinal));

        if (existing is not null)
        {
            existing.Update(name, description);
            return;
        }

        _translations.Add(translation);
    }

    public Result AddKeyword(
        ServiceCategoryKeywordId id, string? value, int weight = 5, string? languageCode = null)
    {
        if (_keywords.Count >= ProviderLimits.MaxCategoryKeywords)
        {
            return Result.Success();
        }

        var keyword = ServiceCategoryKeyword.Create(id, value, weight, languageCode);

        if (keyword is null
            || _keywords.Exists(existing =>
                string.Equals(existing.Normalized, keyword.Normalized, StringComparison.Ordinal)))
        {
            return Result.Success();
        }

        _keywords.Add(keyword);

        return Result.Success();
    }

    /// <summary>Refreshes the denormalised provider count. Called by the sweeper, not by writes.</summary>
    public void SetProviderCount(int count) => ProviderCount = count < 0 ? 0 : count;

    /// <summary>
    /// The name in the caller's language, falling back to the default. Falls back on the
    /// primary subtag too, so a client asking for <c>pt-BR</c> gets the <c>pt</c> translation
    /// rather than English.
    /// </summary>
    public string NameIn(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return Name;
        }

        var code = languageCode.Trim().ToLowerInvariant();

        var exact = _translations.FirstOrDefault(translation =>
            string.Equals(translation.LanguageCode, code, StringComparison.Ordinal));

        if (exact is not null)
        {
            return exact.Name;
        }

        var primary = code.Split('-')[0];

        var partial = _translations.FirstOrDefault(translation =>
            translation.LanguageCode.StartsWith(primary, StringComparison.Ordinal));

        return partial?.Name ?? Name;
    }

    /// <summary>Whether any of this node's names or aliases matches a caller's term.</summary>
    public bool Matches(string? term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return false;
        }

        var normalized = ServiceCategoryAlias.Normalize(term);

        if (normalized.Length == 0)
        {
            return false;
        }

        return string.Equals(ServiceCategoryAlias.Normalize(Name), normalized, StringComparison.Ordinal)
            || string.Equals(ServiceCategoryAlias.Normalize(Slug.Value), normalized, StringComparison.Ordinal)
            || _aliases.Exists(alias =>
                string.Equals(alias.Normalized, normalized, StringComparison.Ordinal));
    }

    /// <summary>Stamped by the shared persistence interceptor. Never called by a handler.</summary>
    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    /// <inheritdoc cref="SetCreated" />
    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    private void RaiseCreated() =>
        RaiseDomainEvent(new ServiceCategoryChangedDomainEvent
        {
            CategoryId = Id,
            Slug = Slug.Value,
            Change = "created",
            ParentId = ParentId
        });

    private static string? Trim(string? value, int max) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim()[..Math.Min(value.Trim().Length, max)];
}
