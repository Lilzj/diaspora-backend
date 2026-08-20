using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Giving;

/// <summary>
/// The caller's own giving.
/// <para>
/// <b>An anonymous gift is absent from this, by construction.</b> The identifier was never stored, so
/// there is nothing to match on — which is the cost of structural anonymity, stated rather than
/// worked around.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.GivingRead)]
public sealed record GetMyGivingQuery : IQuery<GivingHistoryResponse>
{
    /// <summary>
    /// Return gifts made at or before this instant.
    /// <para>
    /// The history is assembled from two sources — donations and contributions — and merged in
    /// memory, so the cursor is a timestamp boundary rather than a keyset. Two gifts sharing the
    /// boundary instant are both served and both re-served on the next page; the client de-duplicates
    /// by identifier, which it must do anyway when merging two sequences.
    /// </para>
    /// </summary>
    public DateTimeOffset? Before { get; init; }

    public int Take { get; init; } = Domain.Policies.FinancialLimits.DefaultLedgerPageSize;
}
