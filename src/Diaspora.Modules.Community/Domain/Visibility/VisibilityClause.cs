using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Visibility;

/// <summary>
/// One constraint in a visibility rule.
/// <para>
/// A kind and a value, both strings, because the set of constraint kinds is open by design: a
/// module registering a visibility evaluator brings its own kinds with it. B6 understands
/// <c>country</c>, <c>area</c>, <c>follower</c>, <c>verified</c> and <c>tier</c>; anything else
/// is passed to the evaluators, and a clause nobody claims is treated as unsatisfiable.
/// </para>
/// <para>
/// Unsatisfiable rather than ignored, and that asymmetry is deliberate. Ignoring an
/// unrecognised constraint widens the audience — which is the one direction a visibility system
/// must never fail in.
/// </para>
/// </summary>
public sealed class VisibilityClause : Entity<VisibilityClauseId>
{
    public const int MaxKindLength = 32;

    public const int MaxValueLength = 200;

    private VisibilityClause(VisibilityClauseId id, string kind, string value, int position)
        : base(id)
    {
        Kind = kind;
        Value = value;
        Position = position;
    }

    private VisibilityClause() { }

    public string Kind { get; private set; } = string.Empty;

    public string Value { get; private set; } = string.Empty;

    public int Position { get; private set; }

    internal static VisibilityClause Create(string kind, string value, int position)
    {
        var normalisedKind = (kind ?? string.Empty).Trim().ToLowerInvariant();
        var normalisedValue = (value ?? string.Empty).Trim();

        return new VisibilityClause(
            VisibilityClauseId.New(),
            normalisedKind.Length > MaxKindLength ? normalisedKind[..MaxKindLength] : normalisedKind,
            normalisedValue.Length > MaxValueLength
                ? normalisedValue[..MaxValueLength]
                : normalisedValue,
            position);
    }
}
