using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Financial.V1.Events;

/// <summary>
/// A community contribution completed.
/// <para>
/// <b><see cref="AmountMinorUnits"/> is null for the four non-monetary kinds, and that is not a
/// zero.</b> Most giving on this platform is not money — goods, volunteer time, professional skills
/// and supplies — and a consumer totalling a fund must be able to tell "gave nothing" from "gave
/// something that is not money".
/// </para>
/// </summary>
public sealed record CommunityContributionCompletedIntegrationEvent : IntegrationEvent
{
    public required Guid ContributionId { get; init; }

    public required Guid FundId { get; init; }

    public required string FundCode { get; init; }

    /// <summary>Null when the contribution is anonymous.</summary>
    public Guid? ContributorUserId { get; init; }

    public required bool IsAnonymous { get; init; }

    /// <summary>
    /// <c>Monetary</c>, <c>Goods</c>, <c>VolunteerService</c>, <c>ProfessionalSkills</c> or
    /// <c>Supplies</c>.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>Null for every kind except <c>Monetary</c>.</summary>
    public long? AmountMinorUnits { get; init; }

    public string? Currency { get; init; }

    /// <summary>How much of what, for a non-monetary contribution. "47 items".</summary>
    public decimal? PledgeQuantity { get; init; }

    public string? PledgeUnit { get; init; }

    public required string ReceiptReference { get; init; }

    public string? CountryCode { get; init; }

    public required DateTimeOffset CompletedAt { get; init; }
}
