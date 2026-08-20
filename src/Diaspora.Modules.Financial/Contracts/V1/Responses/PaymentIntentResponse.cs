namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// A payment, through its whole lifecycle.
/// <para>
/// <b><see cref="NextAction"/> is how a future gateway asks for 3-D Secure without a contract
/// change.</b> It is an opaque string the adapter produced and the platform passed through unread;
/// the client hands it back to the same adapter's SDK. Interpreting it here would be provider
/// knowledge in a module that deliberately has none.
/// </para>
/// </summary>
public sealed record PaymentIntentResponse
{
    public required Guid PaymentIntentId { get; init; }

    /// <summary>
    /// <c>Draft</c>, <c>Pending</c>, <c>Authorized</c>, <c>Captured</c>, <c>Settled</c>,
    /// <c>Failed</c>, <c>Cancelled</c>, <c>Refunded</c>, <c>Expired</c>.
    /// </summary>
    public required string Status { get; init; }

    public required MoneyResponse Amount { get; init; }

    public required MoneyResponse Captured { get; init; }

    public required MoneyResponse Refunded { get; init; }

    /// <summary>What is still refundable.</summary>
    public required MoneyResponse RefundableRemaining { get; init; }

    public required string MethodKind { get; init; }

    /// <summary>
    /// <c>Purchase</c> or <c>Deposit</c> — where the money lands.
    /// <para>
    /// On the wire because a client rendering a payment has to know whether the member is paying the
    /// platform or paying themselves, and inferring it from a description would be inferring it from
    /// a string somebody will translate.
    /// </para>
    /// </summary>
    public required string Purpose { get; init; }

    public required string Description { get; init; }

    public string? SubjectType { get; init; }

    public Guid? SubjectId { get; init; }

    /// <summary>The adapter that handled it. Never a client's business, but useful in support.</summary>
    public string? Provider { get; init; }

    /// <summary>What the client must do next. Opaque; hand it to the adapter's SDK.</summary>
    public string? NextAction { get; init; }

    public string? FailureCode { get; init; }

    public string? FailureMessage { get; init; }

    public required int AttemptCount { get; init; }

    public required IReadOnlyList<RefundResponse> Refunds { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset? AuthorizedAt { get; init; }

    public DateTimeOffset? CapturedAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
