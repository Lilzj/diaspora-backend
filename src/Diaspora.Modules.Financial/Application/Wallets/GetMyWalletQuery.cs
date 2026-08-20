using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Wallets;

/// <summary>
/// The caller's own wallets.
/// <para>
/// Takes no user identifier. A financial endpoint that accepted one would be a financial endpoint
/// somebody would eventually pass somebody else's — every module since B14 takes the same position,
/// and here the stakes are a balance rather than a preference.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.WalletRead)]
public sealed record GetMyWalletQuery : IQuery<IReadOnlyList<WalletResponse>>
{
    /// <summary>Narrow to one currency. Null returns every wallet the caller holds.</summary>
    public string? Currency { get; init; }
}
