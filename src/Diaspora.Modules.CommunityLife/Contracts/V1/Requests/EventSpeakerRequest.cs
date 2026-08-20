namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>Somebody billed to speak. Not a membership and not necessarily a platform account.</summary>
public sealed record EventSpeakerRequest
{
    public required string Name { get; init; }

    /// <summary>What they do — "Immigration Solicitor", "Head of Talent".</summary>
    public string? Title { get; init; }

    /// <summary>Who they are from. Free text, deliberately not a provider reference.</summary>
    public string? Organization { get; init; }

    public string? Biography { get; init; }

    /// <summary>A B5 asset identifier. Never a URL.</summary>
    public Guid? PhotoMediaId { get; init; }

    public Guid? MemberUserId { get; init; }
}
