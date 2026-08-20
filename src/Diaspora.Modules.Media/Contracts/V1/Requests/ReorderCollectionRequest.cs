namespace Diaspora.Modules.Media.Contracts.V1.Requests;

/// <summary>
/// Sets a collection's order outright.
/// <para>
/// All-or-nothing: the list must be exactly the collection's membership. A partial reorder has
/// no defined meaning — three of five identifiers does not say where the other two go — and
/// accepting one produces an order neither side agrees on.
/// </para>
/// </summary>
public sealed record ReorderCollectionRequest
{
    /// <summary>Every asset in the collection, in the order they should render.</summary>
    public required IReadOnlyList<Guid> MediaIds { get; init; }
}
