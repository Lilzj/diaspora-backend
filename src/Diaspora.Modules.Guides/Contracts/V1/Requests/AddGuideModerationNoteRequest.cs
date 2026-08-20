namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>Appends to the guide's editorial audit trail. Append-only; never shown to readers.</summary>
public sealed record AddGuideModerationNoteRequest
{
    public required string Note { get; init; }
}
