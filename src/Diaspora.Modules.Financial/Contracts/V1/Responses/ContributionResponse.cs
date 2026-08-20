namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// One contribution to a community fund.
/// <para>
/// <b><see cref="Amount"/> is null for the four non-monetary kinds and that is not a zero.</b> A
/// pledge of six Saturdays at a food bank has no amount; <see cref="PledgeQuantity"/> and
/// <see cref="PledgeUnit"/> are what it has instead, and a consumer totalling a fund must be able to
/// tell "gave nothing" from "gave something that is not money".
/// </para>
/// </summary>
public sealed record ContributionResponse
{
    public required Guid ContributionId { get; init; }

    public required string FundCode { get; init; }

    /// <summary>Absent on an anonymous contribution.</summary>
    public Guid? ContributorUserId { get; init; }

    public required bool IsAnonymous { get; init; }

    /// <summary>
    /// <c>Monetary</c>, <c>Goods</c>, <c>VolunteerService</c>, <c>ProfessionalSkills</c> or
    /// <c>Supplies</c>.
    /// </summary>
    public required string Kind { get; init; }

    public required string Status { get; init; }

    /// <summary>Null for every kind except <c>Monetary</c>.</summary>
    public MoneyResponse? Amount { get; init; }

    /// <summary>How much of what, for a non-monetary contribution. "47 items".</summary>
    public decimal? PledgeQuantity { get; init; }

    public string? PledgeUnit { get; init; }

    public string? PledgeDescription { get; init; }

    public string? Message { get; init; }

    public required string ReceiptReference { get; init; }

    public string? CountryCode { get; init; }

    public PaymentIntentResponse? Payment { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }
}
