using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Placements;

/// <summary>
/// Stops a placement.
/// <para>
/// Whether the money comes back is the refund-window policy's decision, not this command's — which
/// is why there is no refund flag. An operator refunding outside the window issues one against the
/// payment explicitly, and it is recorded as a goodwill refund rather than as a policy outcome.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.PurchasesCreate)]
[Auditable(Action = "financial.placement.cancel")]
public sealed record CancelPlacementCommand : ICommand<PlacementResponse>
{
    public required Guid PlacementId { get; init; }

    public string? Reason { get; init; }
}
