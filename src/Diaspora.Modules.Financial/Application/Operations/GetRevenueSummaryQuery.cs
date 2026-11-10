using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Operations;

/// <summary>What the platform earned over a period, per currency.</summary>
[RequirePermission(FinancialPermissions.ReportingRead)]
public sealed record GetRevenueSummaryQuery : IQuery<IReadOnlyList<RevenueSummaryResponse>>
{
    public required DateTimeOffset From { get; init; }

    public required DateTimeOffset To { get; init; }

    /// <summary><c>Total</c>, <c>Country</c>, <c>Module</c>, <c>Product</c> or <c>Category</c>.</summary>
    public string? Dimension { get; init; }

    public string? Currency { get; init; }
}
