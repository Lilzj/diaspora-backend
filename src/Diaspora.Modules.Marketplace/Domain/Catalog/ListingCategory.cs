using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Domain.Catalog;

/// <summary>
/// One node in the commerce taxonomy — "Electronics", "Mobile Phones", "Vehicles".
/// <para>
/// <b>Hierarchy by parent key rather than by parent identifier, and by a materialised path rather
/// than by recursion.</b> A contributor declares "mobile-phones lives under electronics" without
/// knowing what identifier electronics was given, which is what lets two independent contributors
/// build one tree. <see cref="Path"/> makes "everything under Electronics" a single indexed prefix
/// match instead of a recursive descent — the same lesson B4 records for geography, applied to a
/// tree that is read on every browse.
/// </para>
/// <para>
/// <b>Retired rather than deleted, always.</b> Listings store the key, so deleting a node would
/// orphan every listing filed under it. <see cref="IsActive"/> is what takes a category off the
/// create form while leaving a decade of listings readable.
/// </para>
/// <para>
/// <b>Not soft-deletable, deliberately.</b> A category is reference data: it is retired with a
/// flag, and a global query filter that hid retired rows would hide them from the very screens
/// that exist to un-retire them.
/// </para>
/// </summary>
public sealed class ListingCategory : AggregateRoot<ListingCategoryId>, IAuditableEntity
{
    private ListingCategory(
        ListingCategoryId id,
        string key,
        string name,
        string? parentKey,
        string path,
        int depth,
        DateTimeOffset createdAt)
        : base(id)
    {
        Key = key;
        Name = name;
        ParentKey = parentKey;
        Path = path;
        Depth = depth;
        IsActive = true;
        CreatedAt = createdAt;
    }

    private ListingCategory() { }

    /// <summary>The stable handle listings store. Lower-case, hyphenated, unique.</summary>
    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>The parent's key. Null at the top of the tree.</summary>
    public string? ParentKey { get; private set; }

    /// <summary>
    /// The materialised path, slash-delimited and ending in this node's key —
    /// <c>/electronics/mobile-phones/</c>. Prefix-matched with <c>LIKE</c>.
    /// </summary>
    public string Path { get; private set; } = string.Empty;

    /// <summary>How deep the node sits, from one. Bounded by the tree's depth limit.</summary>
    public int Depth { get; private set; }

    /// <summary>An icon name the client resolves. The platform ships no images.</summary>
    public string? IconKey { get; private set; }

    /// <summary>Display order within its level.</summary>
    public int Order { get; private set; }

    /// <summary>Whether new listings may be filed here.</summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Live listings filed directly here. Denormalised so a category page can show a count without
    /// a correlated aggregate per node, and adjusted by the same handler that indexes a listing.
    /// </summary>
    public int ListingCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    /// <summary>Whether this node sits beneath another, by path.</summary>
    public bool IsUnder(string? ancestorPath) =>
        !string.IsNullOrEmpty(ancestorPath)
        && Path.StartsWith(ancestorPath, StringComparison.Ordinal)
        && !string.Equals(Path, ancestorPath, StringComparison.Ordinal);

    public static Result<ListingCategory> Create(
        ListingCategoryId id,
        string? key,
        string? name,
        DateTimeOffset now,
        string? parentKey = null,
        string? parentPath = null,
        string? description = null,
        string? iconKey = null,
        int order = 0)
    {
        ArgumentNullException.ThrowIfNull(id);

        var normalizedKey = NormalizeKey(key);

        if (normalizedKey is null)
        {
            return Result.Failure<ListingCategory>(MarketplaceErrors.CategoryKeyInvalid);
        }

        var trimmedName = name?.Trim();

        if (string.IsNullOrEmpty(trimmedName)
            || trimmedName.Length > MarketplaceLimits.MaxDisplayName)
        {
            return Result.Failure<ListingCategory>(MarketplaceErrors.CategoryKeyInvalid);
        }

        var normalizedParent = NormalizeKey(parentKey);

        if (normalizedParent is null && !string.IsNullOrWhiteSpace(parentKey))
        {
            return Result.Failure<ListingCategory>(MarketplaceErrors.CategoryKeyInvalid);
        }

        var prefix = normalizedParent is null
            ? "/"
            : string.IsNullOrWhiteSpace(parentPath) ? $"/{normalizedParent}/" : parentPath;

        var path = $"{prefix}{normalizedKey}/";
        var depth = path.Count(character => character == '/') - 1;

        if (depth > MarketplaceLimits.MaxCategoryDepth)
        {
            return Result.Failure<ListingCategory>(MarketplaceErrors.CategoryDepthExceeded);
        }

        var category = new ListingCategory(
            id, normalizedKey, trimmedName, normalizedParent, path, depth, now)
        {
            Description = Clamp(description, MarketplaceLimits.MaxNoteLength),
            IconKey = Clamp(iconKey, MarketplaceLimits.MaxCategoryKey),
            Order = order
        };

        return Result.Success(category);
    }

    /// <summary>Renames or re-describes the node. The key never moves.</summary>
    public Result Update(string? name, string? description, string? iconKey, int order)
    {
        var trimmedName = name?.Trim();

        if (string.IsNullOrEmpty(trimmedName)
            || trimmedName.Length > MarketplaceLimits.MaxDisplayName)
        {
            return Result.Failure(MarketplaceErrors.CategoryKeyInvalid);
        }

        Name = trimmedName;
        Description = Clamp(description, MarketplaceLimits.MaxNoteLength);
        IconKey = Clamp(iconKey, MarketplaceLimits.MaxCategoryKey);
        Order = order;

        return Result.Success();
    }

    /// <summary>Takes the node off the create form. Existing listings stay exactly where they are.</summary>
    public void Retire() => IsActive = false;

    /// <summary>Puts it back.</summary>
    public void Reinstate() => IsActive = true;

    /// <summary>Adjusts the denormalised count. Floored at zero.</summary>
    public void ApplyListingDelta(int delta) =>
        ListingCount = Math.Max(0, ListingCount + delta);

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    /// <summary>
    /// The canonical key form. Public because the seeder normalises before it compares, so a
    /// contributor supplying "Mobile Phones" and one supplying "mobile-phones" collide on one node
    /// rather than creating two.
    /// </summary>
    public static string? NormalizeKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var normalized = key.Trim().ToLowerInvariant().Replace(' ', '-');

        var valid = normalized.Length <= MarketplaceLimits.MaxCategoryKey
            && normalized.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')
            && !normalized.StartsWith('-')
            && !normalized.EndsWith('-')
            && !normalized.Contains("--", StringComparison.Ordinal);

        return valid ? normalized : null;
    }

    private static string? Clamp(string? value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > maximum ? trimmed[..maximum] : trimmed;
    }
}
