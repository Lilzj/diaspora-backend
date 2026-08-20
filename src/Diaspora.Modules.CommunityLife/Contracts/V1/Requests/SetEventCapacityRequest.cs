namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>Restate an event's ceiling. Refused below what is already committed.</summary>
public sealed record SetEventCapacityRequest
{
    public int? BaseRevision { get; init; }

    /// <summary>Null means uncapped.</summary>
    public int? Capacity { get; init; }
}
