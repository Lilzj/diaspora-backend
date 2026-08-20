namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>
/// One thing a member said about a place.
/// <para>
/// Only approved insights appear on a public surface. The status field exists because an author
/// reading their own submissions and a moderator working the queue both need to see the unapproved
/// ones, and both reach them through paths that opted in deliberately.
/// </para>
/// </summary>
public sealed record NeighborhoodInsightResponse
{
    public required Guid Id { get; init; }

    /// <summary>B4 area the insight is about.</summary>
    public required Guid AreaId { get; init; }

    public required string Category { get; init; }

    public required string Body { get; init; }

    /// <summary>A short chip, where the author gave one.</summary>
    public string? Label { get; init; }

    /// <summary>Submitted, Approved, Rejected or Retired.</summary>
    public required string Status { get; init; }

    /// <summary>How many other members have attested to it. What the profile ranks by.</summary>
    public required int EndorsementCount { get; init; }

    /// <summary>Whether the caller has endorsed it.</summary>
    public required bool HasEndorsed { get; init; }

    /// <summary>Whether the caller wrote it.</summary>
    public required bool IsAuthor { get; init; }

    /// <summary>The moderator reason, present for the author and for staff.</summary>
    public string? DecisionNote { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
