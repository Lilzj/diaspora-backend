using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Domain.Catalog;

/// <summary>
/// One employment domain — Technology, Healthcare, Skilled Trades.
/// <para>
/// <b>Rows rather than an enum, because the brief requires the set to be extensible.</b> The
/// fourteen the platform ships with are seeded from a contributor at startup; a deployment
/// serving a market the platform has not thought about adds its own without a code change, and
/// a module that ships a category tree contributes it through the same seam.
/// </para>
/// <para>
/// <b>Categories are deactivated, never deleted.</b> A posting stores the key, and deleting a
/// category would either orphan every posting under it or force a cascade nobody wants.
/// <see cref="IsActive"/> takes it out of the pickers while leaving history readable.
/// </para>
/// <para>
/// One level of nesting, through <see cref="ParentKey"/>. Deep trees are how category systems
/// become unmaintainable, and two levels covers every real employment taxonomy.
/// </para>
/// </summary>
public sealed class JobCategory : AggregateRoot<JobCategoryId>, IAuditableEntity
{
    private JobCategory(
        JobCategoryId id,
        string key,
        string name,
        string? parentKey,
        int order,
        DateTimeOffset createdAt)
        : base(id)
    {
        Key = key;
        Name = name;
        ParentKey = parentKey;
        Order = order;
        IsActive = true;
        CreatedAt = createdAt;
    }

    private JobCategory() { }

    /// <summary>The stable handle a posting stores. Lower-case, hyphenated, never changes.</summary>
    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>The parent's key, for one level of nesting. Null at the top.</summary>
    public string? ParentKey { get; private set; }

    /// <summary>An icon name the client resolves. The platform ships no images for these.</summary>
    public string? IconKey { get; private set; }

    public int Order { get; private set; }

    /// <summary>Whether it appears in pickers. False retires it without orphaning history.</summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Postings currently under it. Denormalised for the browse page's counts, which would
    /// otherwise be one aggregate query per tile.
    /// </summary>
    public int PostingCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    /// <summary>Normalises free text to a category key, or null when nothing usable survives.</summary>
    public static string? NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var key = value.Trim().ToLowerInvariant();

        var valid = key.Length <= JobLimits.MaxCategoryKey
            && key.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')
            && !key.StartsWith('-')
            && !key.EndsWith('-')
            && !key.Contains("--", StringComparison.Ordinal);

        return valid ? key : null;
    }

    public static Result<JobCategory> Create(
        JobCategoryId id,
        string? key,
        string? name,
        DateTimeOffset now,
        string? parentKey = null,
        string? description = null,
        string? iconKey = null,
        int order = 0)
    {
        ArgumentNullException.ThrowIfNull(id);

        var normalized = NormalizeKey(key);

        if (normalized is null)
        {
            return Result.Failure<JobCategory>(JobErrors.CategoryKeyInvalid);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<JobCategory>(JobErrors.CategoryKeyInvalid);
        }

        var display = name.Trim();

        if (display.Length > JobLimits.MaxDisplayName)
        {
            return Result.Failure<JobCategory>(JobErrors.CategoryKeyInvalid);
        }

        var parent = parentKey is null ? null : NormalizeKey(parentKey);

        // A category that is its own parent is a cycle, and one level of nesting means a
        // parent must itself be a root — which the seeder guarantees and this cannot check.
        if (parent is not null && string.Equals(parent, normalized, StringComparison.Ordinal))
        {
            return Result.Failure<JobCategory>(JobErrors.CategoryKeyInvalid);
        }

        return Result.Success(new JobCategory(id, normalized, display, parent, order, now)
        {
            Description = Clip(description, JobLimits.MaxBulletLength),
            IconKey = Clip(iconKey, JobLimits.MaxDisplayName)
        });
    }

    /// <summary>Renames and re-describes. The key is untouched, so postings stay attached.</summary>
    public Result Update(string? name, string? description, string? iconKey, int order)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(JobErrors.CategoryKeyInvalid);
        }

        var display = name.Trim();

        if (display.Length > JobLimits.MaxDisplayName)
        {
            return Result.Failure(JobErrors.CategoryKeyInvalid);
        }

        Name = display;
        Description = Clip(description, JobLimits.MaxBulletLength);
        IconKey = Clip(iconKey, JobLimits.MaxDisplayName);
        Order = order;

        return Result.Success();
    }

    /// <summary>Takes it out of the pickers. History under it stays readable.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Puts it back.</summary>
    public void Activate() => IsActive = true;

    /// <summary>Adjusts the denormalised count. Floored at zero against double delivery.</summary>
    public void ApplyPostingDelta(int delta) => PostingCount = Math.Max(0, PostingCount + delta);

    /// <summary>Replaces the count outright, for the rebuild sweep.</summary>
    public void SetPostingCount(int count) => PostingCount = Math.Max(0, count);

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

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= max ? trimmed : trimmed[..max].TrimEnd();
    }
}
