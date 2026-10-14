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

    /// <summary>
    /// B4 area the insight is about. Null for an insight written at a point (14 Oct 2026), which
    /// belongs to no catalogue neighbourhood.
    /// </summary>
    public Guid? AreaId { get; init; }

    /// <summary>
    /// The point the insight is about. For a catalogue insight, its neighbourhood's centre. Null
    /// only where that neighbourhood had none.
    /// </summary>
    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>
    /// The Google place name the author chose, as they chose it. Null when they wrote about "around
    /// here" or about a catalogue neighbourhood; never a name the platform inferred.
    /// </summary>
    public string? PlaceName { get; init; }

    /// <summary>The Google place identifier behind <see cref="PlaceName"/>, where one was sent.</summary>
    public string? PlaceId { get; init; }

    /// <summary>The country the point is in, where the author's client said.</summary>
    public string? CountryCode { get; init; }

    /// <summary>
    /// How far the point is from the one a point-keyed list was asked about, in kilometres. Null on
    /// every other read.
    /// </summary>
    public double? DistanceKm { get; init; }

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
