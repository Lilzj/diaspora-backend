namespace Diaspora.Modules.Financial.Contracts.V1.Requests;

/// <summary>Start a subscription.</summary>
public sealed record ActivateSubscriptionRequest
{
    public required string PlanCode { get; init; }

    /// <summary>
    /// Whether to take the plan's trial. Refused when this member has held this plan before —
    /// otherwise the trial is a recurring discount rather than a trial.
    /// </summary>
    public bool WithTrial { get; init; } = true;

    public string? MethodKind { get; init; }

    public string? PromotionCode { get; init; }

    public string? CountryCode { get; init; }

    public string? IdempotencyKey { get; init; }
}
