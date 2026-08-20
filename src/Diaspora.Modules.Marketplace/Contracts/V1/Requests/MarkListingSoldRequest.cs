namespace Diaspora.Modules.Marketplace.Contracts.V1.Requests;

/// <summary>
/// Records a sale the seller made without a reservation behind it.
/// <para>
/// The path most peer-to-peer sales actually take: two people meet, money changes hands, and the
/// seller tells the platform afterwards. Everything is optional because the seller may not want to
/// say, and a required buyer or price would push them into inventing one.
/// </para>
/// </summary>
public sealed record MarkListingSoldRequest
{
    /// <summary>How many units went. Everything left when omitted.</summary>
    public int? Quantity { get; init; }

    /// <summary>Who bought, if the seller wants it recorded and the buyer is a member.</summary>
    public Guid? BuyerUserId { get; init; }

    /// <summary>What was paid. Never inferred from the asking price when omitted.</summary>
    public decimal? Amount { get; init; }

    public string? Currency { get; init; }
}
