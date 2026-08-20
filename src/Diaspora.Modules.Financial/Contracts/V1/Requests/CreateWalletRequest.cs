namespace Diaspora.Modules.Financial.Contracts.V1.Requests;

/// <summary>
/// Provision a financial identity and its wallets.
/// <para>
/// Members get one automatically when Trust approves their verification, so this path exists for
/// organizations, providers and operator recovery — the cases where nothing publishes an event that
/// means "this owner now needs somewhere to keep money".
/// </para>
/// </summary>
public sealed record CreateWalletRequest
{
    /// <summary>The identifier in the module that owns the thing. Never resolved here.</summary>
    public required Guid OwnerId { get; init; }

    /// <summary><c>Personal</c>, <c>Organization</c>, <c>Provider</c> or <c>Business</c>.</summary>
    public required string AccountType { get; init; }

    public string? Currency { get; init; }

    public string? DisplayName { get; init; }

    public string? CountryCode { get; init; }
}
