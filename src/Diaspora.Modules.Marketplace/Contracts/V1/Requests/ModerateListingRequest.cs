namespace Diaspora.Modules.Marketplace.Contracts.V1.Requests;

/// <summary>
/// A moderator's decision about a listing.
/// <para>
/// <b>Every action here is reversible and every one demands a reason.</b> The reason is not
/// bureaucracy: it is what a second moderator reads before overturning the first, and what an
/// appeal is answered from. A hide with no explanation is a decision nobody can review.
/// </para>
/// </summary>
public sealed record ModerateListingRequest
{
    /// <summary>
    /// <c>Flag</c>, <c>Hide</c>, <c>Suspend</c>, <c>Restore</c> or <c>Note</c>.
    /// <para>
    /// <c>Hide</c> means "we are looking at this"; <c>Suspend</c> means "we looked, and it stays
    /// down". The seller is told which.
    /// </para>
    /// </summary>
    public required string Action { get; init; }

    /// <summary>Why. Required for everything but a restore.</summary>
    public string? Reason { get; init; }
}
