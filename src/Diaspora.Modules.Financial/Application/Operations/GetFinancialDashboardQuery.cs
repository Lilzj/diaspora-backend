using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Operations;

/// <summary>The operator's overview of the platform's finances.</summary>
[RequirePermission(FinancialPermissions.ReportingRead)]
public sealed record GetFinancialDashboardQuery : IQuery<FinancialDashboardResponse>
{
    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }
}
