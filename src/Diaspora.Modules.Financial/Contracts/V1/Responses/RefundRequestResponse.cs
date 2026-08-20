namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// A member's request for their money back. Not a refund.
/// <para>
/// The two states the client's <c>RefundStatus</c> union was missing — <c>Requested</c> and
/// <c>Rejected</c> — live here rather than on the refund, because a rejected request never produces a
/// refund to carry a status at all.
/// </para>
/// </summary>
public sealed record RefundRequestResponse
{
    public required Guid RefundRequestId { get; init; }

    public required Guid PaymentIntentId { get; init; }

    /// <summary><c>Requested</c>, <c>Approved</c>, <c>Rejected</c> or <c>Withdrawn</c>.</summary>
    public required string Status { get; init; }

    public required MoneyResponse Amount { get; init; }

    public required string Reason { get; init; }

    /// <summary>What the member said when they asked.</summary>
    public required string Note { get; init; }

    /// <summary>A member-safe explanation of the decision. Never an internal note.</summary>
    public string? DecisionNote { get; init; }

    /// <summary>The refund that resulted, once one was actually issued.</summary>
    public Guid? RefundId { get; init; }

    public required DateTimeOffset RequestedAt { get; init; }

    public DateTimeOffset? DecidedAt { get; init; }
}

/// <summary>
/// A financial receipt, resolvable by its reference alone.
/// <para>
/// <b>By reference and not by owner, deliberately.</b> An anonymous donation stores no donor
/// identifier — that is the point of giving anonymously — so it appears in nobody's history and the
/// receipt reference is the only handle the giver was ever issued. A route that required ownership
/// would make the receipt notification for an anonymous gift lead to a screen that could not find it.
/// </para>
/// <para>
/// <b>What that costs, and why it is acceptable.</b> A reference is a bearer token for one receipt,
/// so the generator makes them long and unguessable and this response carries no donor identity at
/// all — only what was given, to what, and when.
/// </para>
/// </summary>
public sealed record ReceiptResponse
{
    public required string ReceiptReference { get; init; }

    /// <summary><c>Donation</c> or <c>Contribution</c>.</summary>
    public required string Kind { get; init; }

    public required string Status { get; init; }

    public required MoneyResponse Amount { get; init; }

    /// <summary>What it was given to. A cause code or a fund code.</summary>
    public string? DestinationCode { get; init; }

    public string? DestinationName { get; init; }

    /// <summary>True when no giver was recorded. There is nothing to have withheld.</summary>
    public required bool IsAnonymous { get; init; }

    public string? CountryCode { get; init; }

    public required DateTimeOffset IssuedAt { get; init; }
}
