using Diaspora.Modules.Financial.Domain.Enums;

namespace Diaspora.Modules.Financial.Extensibility.Catalog;

/// <summary>One policy version a module wants to exist at startup.</summary>
public sealed record FinancialPolicySeed
{
    public required string PolicyKey { get; init; }

    public required FinancialPolicyKind Kind { get; init; }

    public required string Description { get; init; }

    public PolicyScopeKind Scope { get; init; } = PolicyScopeKind.Platform;

    public string? ScopeValue { get; init; }

    /// <summary>The rule's inputs. A percentage, a flat amount, a window in days.</summary>
    public required IReadOnlyDictionary<string, string> Parameters { get; init; }

    /// <summary>A policy the platform guarantees and a deployment may not retire.</summary>
    public bool IsSystem { get; init; }
}
