using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Wallets;

/// <summary>
/// Opens a financial identity and its wallets for an owner.
/// <para>
/// <b>Audited, because provisioning by hand is the path that bypasses the automatic one.</b> A
/// member's identity is opened when Trust approves their verification; this exists for
/// organizations, providers and recovery, and every use of it is somebody deciding an owner should
/// have somewhere to keep money without an event having said so.
/// </para>
/// </summary>
[RequirePermission(FinancialPermissions.WalletManage)]
[Auditable(Action = "financial.wallet.provision")]
public sealed record ProvisionWalletCommand : ICommand<IReadOnlyList<WalletResponse>>
{
    public required Guid OwnerId { get; init; }

    public required string AccountType { get; init; }

    public string? Currency { get; init; }

    public string? DisplayName { get; init; }

    public string? CountryCode { get; init; }
}
