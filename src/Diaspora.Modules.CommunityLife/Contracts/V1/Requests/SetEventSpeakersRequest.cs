namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>Replace an event's speaker list wholesale.</summary>
public sealed record SetEventSpeakersRequest
{
    public int? BaseRevision { get; init; }

    public IReadOnlyList<EventSpeakerRequest> Speakers { get; init; } = [];
}
