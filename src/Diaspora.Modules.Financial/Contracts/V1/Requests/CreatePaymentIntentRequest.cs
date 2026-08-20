namespace Diaspora.Modules.Financial.Contracts.V1.Requests;

/// <summary>
/// Start paying for something.
/// <para>
/// <b>No instrument appears here.</b> A card number, a token or a mandate is collected by the
/// adapter's own SDK on the client and never travels through the platform, which is why the platform
/// cannot store, log or accidentally serialise one.
/// </para>
/// </summary>
public sealed record CreatePaymentIntentRequest
{
    /// <summary>Major units, as the client already renders every price.</summary>
    public required decimal Amount { get; init; }

    /// <summary>ISO 4217, upper case.</summary>
    public required string Currency { get; init; }

    /// <summary><c>Card</c>, <c>BankTransfer</c>, <c>MobileMoney</c>, <c>Wallet</c>, <c>Credit</c>.</summary>
    public required string MethodKind { get; init; }

    public required string Description { get; init; }

    public string? SubjectType { get; init; }

    public Guid? SubjectId { get; init; }

    public string? CountryCode { get; init; }

    /// <summary>
    /// The caller's own key. Uniquely indexed, so a double-tapped pay button is refused by the
    /// database rather than by a check somebody can refactor past.
    /// </summary>
    public string? IdempotencyKey { get; init; }
}
