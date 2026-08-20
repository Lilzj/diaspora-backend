using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Events;

/// <summary>
/// A community contribution completed.
/// <para>
/// <see cref="AmountMinorUnits"/> is null for the four non-monetary kinds, and that is deliberate
/// rather than a zero: a pledge of six Saturdays has no amount, and a consumer totalling a fund must
/// be able to tell "gave nothing" from "gave something that is not money".
/// </para>
/// </summary>
public sealed record CommunityContributionCompletedDomainEvent : DomainEvent
{
    public required CommunityContributionId ContributionId { get; init; }

    public required ContributionFundId FundId { get; init; }

    public required string FundCode { get; init; }

    /// <summary>Null when the contribution is anonymous.</summary>
    public Guid? ContributorUserId { get; init; }

    public required bool IsAnonymous { get; init; }

    public required ContributionKind Kind { get; init; }

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
