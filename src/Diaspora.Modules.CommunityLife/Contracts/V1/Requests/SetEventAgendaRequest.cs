namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>Replace an event's programme wholesale.</summary>
public sealed record SetEventAgendaRequest
{
    public int? BaseRevision { get; init; }

    public IReadOnlyList<AgendaItemRequest> Items { get; init; } = [];
}
