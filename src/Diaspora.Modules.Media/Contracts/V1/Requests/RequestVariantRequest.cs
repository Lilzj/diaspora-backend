namespace Diaspora.Modules.Media.Contracts.V1.Requests;

/// <summary>
/// Asks for a rendition that does not exist yet.
/// <para>
/// A named rung of the ladder, not an arbitrary size. Arbitrary widths mean an attacker can
/// mint unbounded renditions of a fifteen-megabyte photograph with a query string, and the
/// storage bill arrives before the alert does. A closed set keeps the cache key space finite
/// and makes every rendition generated exactly once.
/// </para>
/// </summary>
public sealed record RequestVariantRequest
{
    /// <summary>
    /// <c>thumbnail</c>, <c>small</c>, <c>medium</c>, <c>large</c>, <c>preview</c>,
    /// <c>poster</c>, <c>transcoded</c>.
    /// </summary>
    public required string Variant { get; init; }
}
