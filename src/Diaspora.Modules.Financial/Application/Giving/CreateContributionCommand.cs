using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Giving;

/// <summary>
/// Contributes to a community fund — money, goods, time or skills.
/// <para>
/// <b>Transactional, because a monetary contribution writes the contribution and a payment
/// intent.</b> A non-monetary one writes only the contribution and completes immediately: nothing
/// else has to happen for a pledge to count.
/// </para>
/// </summary>
[RequirePermission(FinancialPermissions.GivingCreate)]
[Transactional(Reason = "A monetary contribution writes the contribution and its payment intent.")]
[Auditable(Action = "financial.contribution.create")]
public sealed record CreateContributionCommand : ICommand<ContributionResponse>
{
    public required string FundCode { get; init; }

    public required string Kind { get; init; }

    public decimal? Amount { get; init; }

    public string? Currency { get; init; }

    public decimal? PledgeQuantity { get; init; }

    public string? PledgeUnit { get; init; }

    public string? PledgeDescription { get; init; }

    public bool IsAnonymous { get; init; }

    public string? Message { get; init; }

    public string? MethodKind { get; init; }

    public string? CountryCode { get; init; }

    public string? IdempotencyKey { get; init; }
}
