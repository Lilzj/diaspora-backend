using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Placements;

/// <summary>Everything the caller has bought a placement for.</summary>
[RequirePermission(FinancialPermissions.PurchasesRead)]
public sealed record GetMyPlacementsQuery : IQuery<IReadOnlyList<PlacementResponse>>
{
    public string? Status { get; init; }

    /// <summary>Return placements created at or before this instant. See ADR-017.</summary>
    public DateTimeOffset? Before { get; init; }

    /// <summary>Identifiers already served at the boundary instant.</summary>
    public IReadOnlyList<Guid> ExcludeIds { get; init; } = [];

    public int Take { get; init; } = Domain.Policies.FinancialLimits.DefaultLedgerPageSize;
}
