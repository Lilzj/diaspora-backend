using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.ValueObjects;

/// <summary>
/// One condition a buyer must satisfy before a product is offered to them.
/// <para>
/// Rules live on the catalogue row rather than in the code that reads it, for the reason B8.2
/// recorded about notice categories: a hard-coded list means the deployment finds out at the worst
/// possible moment. Restricting a credit bundle to two countries is an operator action.
/// </para>
/// <para>
/// <see cref="RuleId"/> is caller-supplied; the configuration declares <c>ValueGeneratedNever()</c>.
/// </para>
/// </summary>
public sealed class EligibilityRule : ValueObject
{
    private EligibilityRule(Guid ruleId, EligibilityRuleKind kind, string value)
    {
        RuleId = ruleId;
        Kind = kind;
        Value = value;
    }

    private EligibilityRule() { }

    public Guid RuleId { get; private set; }

    public EligibilityRuleKind Kind { get; private set; }

    /// <summary>
    /// What the rule compares against. A tier name, a comma-separated country list, a plan code, or
    /// whatever a custom rule implementation agreed with the operator who configured it.
    /// </summary>
    public string Value { get; private set; } = string.Empty;

    public static EligibilityRule Create(Guid ruleId, EligibilityRuleKind kind, string? value) =>
        new(ruleId, kind, value?.Trim() ?? string.Empty);

    /// <summary>Whether a candidate value appears in this rule's comma-separated list.</summary>
    public bool ListContains(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        return Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(candidate.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return RuleId;
        yield return Kind;
        yield return Value;
    }
}
