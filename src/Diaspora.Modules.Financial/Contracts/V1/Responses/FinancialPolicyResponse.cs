namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// One version of one financial rule.
/// <para>
/// <see cref="VersionLabel"/> is what appears on every ledger entry the version priced, which is
/// what makes a historical amount checkable against the rule that produced it rather than against
/// whatever the catalogue says today.
/// </para>
/// </summary>
public sealed record FinancialPolicyResponse
{
    public required Guid PolicyId { get; init; }

    public required string PolicyKey { get; init; }

    public required string Kind { get; init; }

    /// <summary><c>Platform</c>, <c>Country</c>, <c>Module</c> or <c>Product</c>.</summary>
    public required string Scope { get; init; }

    public string? ScopeValue { get; init; }

    public required int Version { get; init; }

    /// <summary><c>marketplace.commission@2</c>.</summary>
    public required string VersionLabel { get; init; }

    public required string Description { get; init; }

    public required bool IsActive { get; init; }

    /// <summary>A version the platform guarantees and a deployment may not retire.</summary>
    public required bool IsSystem { get; init; }

    public required IReadOnlyDictionary<string, string> Parameters { get; init; }

    public required DateTimeOffset EffectiveFrom { get; init; }

    public DateTimeOffset? EffectiveTo { get; init; }
}
