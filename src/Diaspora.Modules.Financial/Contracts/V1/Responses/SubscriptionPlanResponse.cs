namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// One plan in the catalogue.
/// <para>
/// <b>The client renders <see cref="Entitlements"/> and never <see cref="Code"/>.</b> A screen
/// switching on a plan code has to ship again the day somebody inserts a plan between two existing
/// ones; a screen rendering capability keys never does.
/// </para>
/// </summary>
public sealed record SubscriptionPlanResponse
{
    public required Guid PlanId { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string Tier { get; init; }

    public required string Interval { get; init; }

    public required MoneyResponse Price { get; init; }

    public required bool IsFree { get; init; }

    public required int TrialDays { get; init; }

    public required string AudienceType { get; init; }

    public string? CountryCode { get; init; }

    /// <summary>Capability keys with optional limits. What a client actually renders.</summary>
    public required IReadOnlyList<EntitlementResponse> Entitlements { get; init; }

    public required int SortOrder { get; init; }
}
