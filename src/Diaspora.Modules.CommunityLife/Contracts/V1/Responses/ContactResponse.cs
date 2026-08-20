namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>How to reach an organizer or an organization.</summary>
public sealed record ContactResponse
{
    public string? Phone { get; init; }

    public string? WhatsApp { get; init; }

    public string? Email { get; init; }

    public string? Website { get; init; }

    public string? ContactName { get; init; }
}
