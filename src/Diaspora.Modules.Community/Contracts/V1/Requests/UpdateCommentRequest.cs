namespace Diaspora.Modules.Community.Contracts.V1.Requests;

/// <summary>An edit to a comment. Always marked edited — see the aggregate for why.</summary>
public sealed record UpdateCommentRequest
{
    public required string Body { get; init; }
}
