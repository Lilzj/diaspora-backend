using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Operations;

/// <summary>
/// Adds or changes a catalogue entry.
/// <para>
/// <b>This is how a premium feature is created on this platform, and there is no other way.</b> No
/// code path anywhere hard-codes a product, a price or a placement weight.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.CatalogManage)]
[Auditable(Action = "financial.catalog.upsert")]
public sealed record UpsertMonetizationProductCommand : ICommand<MonetizationProductResponse>
{
    public required string Code { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string Category { get; init; }

    public required string SubjectType { get; init; }

    public decimal? PriceAmount { get; init; }

    public string? Currency { get; init; }

    public string? PricingPolicyKey { get; init; }

    public int? DurationDays { get; init; }

    public string? PlacementKind { get; init; }

    public decimal? PriorityWeight { get; init; }

    public bool IsRenewable { get; init; } = true;

    public int SortOrder { get; init; } = 100;

    public IReadOnlyDictionary<string, string>? Eligibility { get; init; }

    public bool Activate { get; init; } = true;

    public DateTimeOffset? AvailableFrom { get; init; }

    public DateTimeOffset? AvailableTo { get; init; }
}
