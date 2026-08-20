using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Domain.Categories;

/// <summary>
/// One node of the guide catalogue.
/// <para>
/// Guides reference categories by <see cref="Key"/> rather than by id, because the key is the
/// contract: it is what a contributor seed declares, what a URL carries and what a frontend
/// switches on, and it survives a reseed where a generated id would not. Which is also why a
/// category is <see cref="Deactivate">deactivated</see> and never deleted — thousands of guides
/// hold the key, and a deleted category would strand every one of them.
/// </para>
/// </summary>
public sealed class GuideCategory : AggregateRoot<GuideCategoryId>, IAuditableEntity
{
    private GuideCategory(
        GuideCategoryId id,
        string key,
        string name,
        string? description,
        string? icon,
        int order,
        string? parentKey,
        DateTimeOffset createdAt)
        : base(id)
    {
        Key = key;
        Name = name;
        Description = description;
        Icon = icon;
        Order = order;
        ParentKey = parentKey;
        IsActive = true;
        CreatedAt = createdAt;
    }

    private GuideCategory() { }

    /// <summary>The stable, seedable identity. Lower-case kebab, unique.</summary>
    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>An icon name the client resolves. Presentation data, stored as text.</summary>
    public string? Icon { get; private set; }

    public int Order { get; private set; }

    /// <summary>Whether new guides may choose it. Existing guides keep it either way.</summary>
    public bool IsActive { get; private set; }

    /// <summary>The parent's key, for a shallow hierarchy. Null at the root.</summary>
    public string? ParentKey { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static Result<GuideCategory> Create(
        GuideCategoryId id,
        string? key,
        string? name,
        DateTimeOffset now,
        string? description = null,
        string? icon = null,
        int order = 0,
        string? parentKey = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        var normalizedKey = NormalizeKey(key);

        if (normalizedKey is null)
        {
            return Result.Failure<GuideCategory>(GuideErrors.CategoryKeyInvalid);
        }

        var trimmedName = name?.Trim();

        if (string.IsNullOrEmpty(trimmedName) || trimmedName.Length > GuideLimits.MaxCategoryName)
        {
            return Result.Failure<GuideCategory>(GuideErrors.CategoryKeyInvalid);
        }

        return Result.Success(new GuideCategory(
            id,
            normalizedKey,
            trimmedName,
            Trim(description, GuideLimits.MaxCategoryDescription),
            Trim(icon, GuideLimits.MaxCategoryName),
            order,
            NormalizeKey(parentKey),
            now));
    }

    /// <summary>Renames or re-describes the category. The key never changes — it is the contract.</summary>
    public Result Update(string? name, string? description, string? icon, int order, string? parentKey)
    {
        var trimmedName = name?.Trim();

        if (string.IsNullOrEmpty(trimmedName) || trimmedName.Length > GuideLimits.MaxCategoryName)
        {
            return Result.Failure(GuideErrors.CategoryKeyInvalid);
        }

        var normalizedParent = parentKey is null ? null : NormalizeKey(parentKey);

        if (parentKey is not null && normalizedParent is null)
        {
            return Result.Failure(GuideErrors.CategoryKeyInvalid);
        }

        // A category cannot sit beneath itself — the one cycle a two-level check can create.
        if (string.Equals(normalizedParent, Key, StringComparison.Ordinal))
        {
            return Result.Failure(GuideErrors.CategoryKeyInvalid);
        }

        Name = trimmedName;
        Description = Trim(description, GuideLimits.MaxCategoryDescription);
        Icon = Trim(icon, GuideLimits.MaxCategoryName);
        Order = order;
        ParentKey = normalizedParent;

        return Result.Success();
    }

    /// <summary>
    /// Retires the category from the picker. Not a delete — guides reference keys, and a
    /// deleted category would strand every guide that ever chose it.
    /// </summary>
    public Result Deactivate()
    {
        IsActive = false;

        return Result.Success();
    }

    /// <summary>Returns a retired category to the picker.</summary>
    public Result Reactivate()
    {
        IsActive = true;

        return Result.Success();
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

    private static string? NormalizeKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var candidate = key.Trim().ToLowerInvariant();

        var valid = candidate.Length <= GuideLimits.MaxCategoryKey
            && candidate.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')
            && !candidate.StartsWith('-')
            && !candidate.EndsWith('-')
            && !candidate.Contains("--", StringComparison.Ordinal);

        return valid ? candidate : null;
    }

    private static string? Trim(string? value, int max) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim()[..Math.Min(value.Trim().Length, max)];
}
