using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Domain.Catalog;

/// <summary>
/// One skill in the platform's catalogue.
/// <para>
/// <b>The catalogue is an aid, not a gate.</b> A posting or a profile may name a skill that is
/// not in it — refusing the save would make a taxonomy nobody can keep current into an obstacle
/// to hiring. What the catalogue buys is the autocomplete that stops "React", "ReactJS" and
/// "react.js" becoming three skills in the first place, and the aliases that reunite them when
/// they already have.
/// </para>
/// <para>
/// <b><see cref="UsageCount"/> is what makes it self-maintaining.</b> Uncatalogued skills named
/// often enough are promoted into the catalogue by a sweep, so the taxonomy grows out of what
/// employers and candidates actually write rather than out of what someone guessed in advance.
/// </para>
/// </summary>
public sealed class JobSkill : AggregateRoot<JobSkillId>, IAuditableEntity
{
    private readonly List<JobSkillAlias> _aliases = [];

    private JobSkill(
        JobSkillId id, string key, string name, string? categoryKey, DateTimeOffset createdAt)
        : base(id)
    {
        Key = key;
        Name = name;
        CategoryKey = categoryKey;
        IsActive = true;
        CreatedAt = createdAt;
    }

    private JobSkill() { }

    /// <summary>The normalised match key. Shared with both sides through the same normaliser.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>The canonical display name. What autocomplete offers.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>The employment domain it belongs to, when it belongs to just one.</summary>
    public string? CategoryKey { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>How many postings and profiles name it. Drives promotion and ordering.</summary>
    public int UsageCount { get; private set; }

    /// <summary>Other spellings that resolve to this skill.</summary>
    public IReadOnlyList<JobSkillAlias> Aliases => _aliases.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static Result<JobSkill> Create(
        JobSkillId id,
        string? name,
        DateTimeOffset now,
        string? categoryKey = null,
        IReadOnlyList<string>? aliases = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        var key = JobSkillRequirement.NormalizeKey(name);

        if (key is null)
        {
            return Result.Failure<JobSkill>(JobErrors.SkillKeyInvalid);
        }

        var display = name!.Trim();

        if (display.Length > JobLimits.MaxDisplayName)
        {
            display = display[..JobLimits.MaxDisplayName].TrimEnd();
        }

        var skill = new JobSkill(
            id, key, display, JobCategory.NormalizeKey(categoryKey), now);

        foreach (var alias in aliases ?? [])
        {
            skill.AddAlias(alias);
        }

        return Result.Success(skill);
    }

    /// <summary>
    /// Adds an alternative spelling. Idempotent, and an alias equal to the key is dropped —
    /// it would resolve to itself and buy nothing.
    /// </summary>
    public Result AddAlias(string? alias)
    {
        var key = JobSkillRequirement.NormalizeKey(alias);

        if (key is null)
        {
            return Result.Failure(JobErrors.SkillKeyInvalid);
        }

        if (string.Equals(key, Key, StringComparison.Ordinal)
            || _aliases.Exists(existing =>
                string.Equals(existing.AliasKey, key, StringComparison.Ordinal)))
        {
            return Result.Success();
        }

        _aliases.Add(JobSkillAlias.Create(key));

        return Result.Success();
    }

    /// <summary>Renames the skill. The key is untouched, so nothing loses its match.</summary>
    public Result Rename(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(JobErrors.SkillKeyInvalid);
        }

        var display = name.Trim();

        Name = display.Length <= JobLimits.MaxDisplayName
            ? display
            : display[..JobLimits.MaxDisplayName].TrimEnd();

        return Result.Success();
    }

    /// <summary>Takes it out of autocomplete. Postings and profiles naming it are untouched.</summary>
    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    /// <summary>Adjusts the usage count. Floored at zero against double delivery.</summary>
    public void ApplyUsageDelta(int delta) => UsageCount = Math.Max(0, UsageCount + delta);

    /// <summary>Replaces the count outright, for the rebuild sweep.</summary>
    public void SetUsageCount(int count) => UsageCount = Math.Max(0, count);

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
}
