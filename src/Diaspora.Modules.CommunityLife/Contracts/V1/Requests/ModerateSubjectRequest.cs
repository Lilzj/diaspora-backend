namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>Hide, suspend, clear or annotate an event or an organization.</summary>
public sealed record ModerateSubjectRequest
{
    /// <summary><c>Hide</c>, <c>Suspend</c>, <c>Clear</c>, <c>Note</c>.</summary>
    public required string Action { get; init; }

    public string? Reason { get; init; }
}
