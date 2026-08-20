namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>
/// How much of the item there is.
/// <para>
/// <see cref="Reserved"/> and <see cref="Sold"/> are sent to everybody, and that is deliberate: a
/// buyer deciding whether to make the journey deserves to know that three of the four are already
/// spoken for. It is also the honest way to render "1 left" without the platform inventing
/// urgency.
/// </para>
/// </summary>
public sealed record ListingInventoryResponse
{
    /// <summary>Units the seller has, in total.</summary>
    public required int Quantity { get; init; }

    /// <summary>Units a buyer could commit to right now.</summary>
    public required int Available { get; init; }

    /// <summary>Units held by a live reservation.</summary>
    public required int Reserved { get; init; }

    /// <summary>Units handed over.</summary>
    public required int Sold { get; init; }

    /// <summary>Whether the platform counts stock down at all.</summary>
    public required bool IsTracked { get; init; }
}
