namespace Diaspora.Modules.Recommendations.Contracts.V1.Requests;

/// <summary>
/// What a member did with a recommendation.
/// <para>
/// <b><see cref="SetId"/> is optional and the handler works hard when it is absent.</b> A client that
/// kept a card from a page whose set has since expired, or an older app version that does not send
/// it, still deserves to have its dismissal honoured — so the handler looks the set up by target
/// rather than refusing. Sending it is much cheaper and much more accurate.
/// </para>
/// <para>
/// <b>An acceptance is reported, never inferred.</b> This module cannot see what happened after a
/// tap, and inferring acceptance from a click would make every strategy look equally good — hiding
/// the only distinction the analytics exist to draw.
/// </para>
/// </summary>
public sealed record RecordFeedbackRequest
{
    /// <summary><c>Viewed</c>, <c>Clicked</c>, <c>Accepted</c>, <c>Dismissed</c> or <c>Bookmarked</c>.</summary>
    public required string Kind { get; init; }

    public required string TargetType { get; init; }

    public required Guid TargetId { get; init; }

    /// <summary>Which surface the card was on.</summary>
    public required string Surface { get; init; }

    /// <summary>The generating set. Optional but strongly preferred — see the class summary.</summary>
    public Guid? SetId { get; init; }

    /// <summary>Where the card sat on the page. Position bias is real and this is how it is measured.</summary>
    public int? Position { get; init; }

    public DateTimeOffset? OccurredAt { get; init; }
}
