namespace Diaspora.Modules.Financial.Contracts.V1.Requests;

/// <summary>
/// Publish a new version of a financial rule. Staff only.
/// <para>
/// <b>There is no "update policy" request, on purpose.</b> Publishing inserts a new version and
/// closes the previous one's window; the old version stays in the table for ever, because every
/// ledger entry it priced names it. An edit would silently rewrite history.
/// </para>
/// </summary>
public sealed record PublishFinancialPolicyRequest
{
    public required string PolicyKey { get; init; }

    /// <summary><c>MarketplaceCommission</c>, <c>PlatformFee</c>, <c>BoostPricing</c>, and so on.</summary>
    public required string Kind { get; init; }

    /// <summary><c>Platform</c>, <c>Country</c>, <c>Module</c> or <c>Product</c>.</summary>
    public string? Scope { get; init; }

    /// <summary>The country code, module or product code the version narrows to.</summary>
    public string? ScopeValue { get; init; }

    public required string Description { get; init; }

    /// <summary>The rule's inputs. A percentage, a flat amount, a window in hours.</summary>
    public required IReadOnlyDictionary<string, string> Parameters { get; init; }

    /// <summary>When it takes effect. Null means immediately.</summary>
    public DateTimeOffset? EffectiveFrom { get; init; }
}
