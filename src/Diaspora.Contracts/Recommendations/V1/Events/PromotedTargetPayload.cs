namespace Diaspora.Contracts.Recommendations.V1.Events;

/// <summary>
/// One subject B14 served <em>because it was promoted</em>.
/// <para>
/// <b>This is the distinction MON-GAP-002 exists on.</b> The register is explicit that a views
/// number taken from a listing's own counter "would answer a different question than how many
/// people saw this <em>because</em> it was promoted". B14 knows which it is: an item whose
/// explanation carries the <c>Promoted</c> reason code ranked where it did because somebody paid,
/// and one that did not, did not.
/// </para>
/// <para>
/// Carried on the generated event so B15 can count impressions without asking B14 anything on the
/// read path. A synchronous call there would put a Financial outage inside somebody's search.
/// </para>
/// </summary>
public sealed record PromotedTargetPayload
{
    /// <summary>B14's target type name — Listing, Provider, Guide, Job, Event, Organization.</summary>
    public required string TargetType { get; init; }

    public required Guid TargetId { get; init; }

    /// <summary>Where it appeared in the set. 1 is the top.</summary>
    public required int Position { get; init; }
}
