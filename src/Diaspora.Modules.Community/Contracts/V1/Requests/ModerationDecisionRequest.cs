namespace Diaspora.Modules.Community.Contracts.V1.Requests;

/// <summary>A moderator's decision.</summary>
public sealed record ModerationDecisionRequest
{
    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    /// <summary>A <c>ModerationActionType</c> name. The client's queue buttons all map.</summary>
    public required string Action { get; init; }

    public string? Reason { get; init; }

    /// <summary>The report being answered, when the decision answers one.</summary>
    public Guid? ReportId { get; init; }
}
