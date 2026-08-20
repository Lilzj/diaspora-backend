namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// One thing that can be bought.
/// <para>
/// <b><see cref="UnavailableReason"/> is why a member sees a greyed-out product with an explanation
/// instead of a product that has vanished.</b> "Verify your identity to buy this" is a conversion
/// path; a missing row is a support ticket.
/// </para>
/// </summary>
public sealed record MonetizationProductResponse
{
    public required Guid ProductId { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string Category { get; init; }

    public required string SubjectType { get; init; }

    public required string Status { get; init; }

    /// <summary>Null when a pricing policy decides the price for this request.</summary>
    public MoneyResponse? Price { get; init; }

    /// <summary>How long what is bought lasts. Null for a one-off with no duration.</summary>
    public int? DurationDays { get; init; }

    /// <summary>What placement this grants, when it grants one.</summary>
    public string? PlacementKind { get; init; }

    public required decimal PriorityWeight { get; init; }

    public required bool IsRenewable { get; init; }

    /// <summary>Whether the caller may buy it right now.</summary>
    public required bool IsAvailable { get; init; }

    /// <summary>A code the client can translate. Null when available.</summary>
    public string? UnavailableReasonCode { get; init; }

    /// <summary>A sentence a member can act on. Null when available.</summary>
    public string? UnavailableReason { get; init; }

    public required int SortOrder { get; init; }
}
