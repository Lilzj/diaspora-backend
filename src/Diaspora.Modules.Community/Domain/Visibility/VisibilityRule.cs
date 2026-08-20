using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Visibility;

/// <summary>
/// A named, reusable audience.
/// <para>
/// What <see cref="VisibilityLevel.Custom"/> points at. A member or an organization defines
/// "my street", "verified traders in Lagos", "everyone who follows me and is in the UK" once,
/// and every post can then name it — which is the difference between custom audiences being a
/// feature and being a per-post form nobody fills in twice.
/// </para>
/// <para>
/// The clauses are stored as an ordered list of typed constraints rather than as an expression
/// string. An expression would need a parser and an evaluator, and a parser over member-supplied
/// text that decides who can read something is a security surface nobody asked for. Constraints
/// compose by intersection: every one must hold.
/// </para>
/// </summary>
public sealed class VisibilityRule : AggregateRoot<VisibilityRuleId>, IAuditableEntity
{
    public const int MaxNameLength = 100;

    public const int MaxClauses = 10;

    private readonly List<VisibilityClause> _clauses = [];

    private VisibilityRule(
        VisibilityRuleId id, UserId ownerId, string name, VisibilityLevel baseLevel) : base(id)
    {
        OwnerId = ownerId;
        Name = name;
        BaseLevel = baseLevel;
    }

    private VisibilityRule() { }

    public UserId OwnerId { get; private set; } = default!;

    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// The widest this rule can ever be. Clauses narrow from here and can never widen past it,
    /// which is what makes a rule safe to evaluate without re-checking the post's own audience.
    /// </summary>
    public VisibilityLevel BaseLevel { get; private set; }

    /// <summary>True for a rule a whole organization shares rather than one member.</summary>
    public bool IsShared { get; private set; }

    public IReadOnlyList<VisibilityClause> Clauses =>
        _clauses.OrderBy(clause => clause.Position).ToList().AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    public static VisibilityRule Create(UserId ownerId, string name, VisibilityLevel baseLevel)
    {
        ArgumentNullException.ThrowIfNull(ownerId);

        var trimmed = (name ?? string.Empty).Trim();

        return new VisibilityRule(
            VisibilityRuleId.New(),
            ownerId,
            trimmed.Length > MaxNameLength ? trimmed[..MaxNameLength] : trimmed,
            baseLevel);
    }

    public void AddClause(string kind, string value)
    {
        if (_clauses.Count >= MaxClauses)
        {
            return;
        }

        _clauses.Add(VisibilityClause.Create(kind, value, _clauses.Count));
    }

    public void ClearClauses() => _clauses.Clear();

    public void SetShared(bool shared) => IsShared = shared;

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
