namespace Diaspora.Modules.Community.Contracts.V1.Requests;

/// <summary>Adding or withdrawing a reaction.</summary>
public sealed record ReactRequest
{
    /// <summary>
    /// A catalogue key, for example <c>helpful</c> or <c>confirmed</c>. Not an enum: the
    /// catalogue is data, and a client can read the available kinds from the capabilities route.
    /// </summary>
    public required string ReactionKey { get; init; }

    public string? ContentType { get; init; }

    public Guid? ContentId { get; init; }
}
