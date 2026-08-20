using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Giving;

/// <summary>The community funds somebody can contribute to.</summary>
[Authorize(Permission = FinancialPermissions.GivingRead)]
public sealed record GetContributionFundsQuery : IQuery<IReadOnlyList<ContributionFundResponse>>
{
    public string? CountryCode { get; init; }
}
