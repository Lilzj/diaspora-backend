namespace Diaspora.Modules.Marketplace.Contracts.V1.Requests;

/// <summary>
/// Bars a seller from trading, or lets them back.
/// <para>
/// <b>The seller's listings go dark and their own state is untouched</b>, so reinstating brings
/// back exactly what was there. A suspension that archived four hundred listings would be one
/// nobody could ever undo.
/// </para>
/// </summary>
public sealed record SuspendSellerRequest
{
    /// <summary><c>Suspend</c> or <c>Reinstate</c>.</summary>
    public required string Action { get; init; }

    /// <summary>Why. Required to suspend; shown to the seller and to nobody else.</summary>
    public string? Reason { get; init; }
}
