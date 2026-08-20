namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>
/// How many people can come, and how many said they would.
/// <para>
/// Totals only. Who is coming is the most socially sensitive thing this module holds, and is served
/// from the attendee endpoint under the organization's own visibility rules.
/// </para>
/// </summary>
public sealed record EventCapacityResponse
{
    /// <summary>Null means uncapped.</summary>
    public int? Maximum { get; init; }

    public required int GoingCount { get; init; }

    public required int InterestedCount { get; init; }

    public required int WaitlistCount { get; init; }

    /// <summary>Rendered to organizers. A live attendance figure at the door.</summary>
    public int? CheckedInCount { get; init; }

    public int? RemainingPlaces { get; init; }

    public required bool IsFull { get; init; }
}
