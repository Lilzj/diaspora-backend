namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>Schedules a draft to publish itself at a future instant. Future only, drafts only.</summary>
public sealed record ScheduleGuidePublicationRequest
{
    public required DateTimeOffset PublishAt { get; init; }
}
