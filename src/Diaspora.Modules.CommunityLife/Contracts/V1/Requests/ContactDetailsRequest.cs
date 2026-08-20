namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>How to reach an organizer or an organization.</summary>
public sealed record ContactDetailsRequest
{
    public string? Phone { get; init; }

    /// <summary>
    /// Its own field rather than a note. Across most of the communities this platform serves, a
    /// WhatsApp number is the contact somebody will actually use.
    /// </summary>
    public string? WhatsApp { get; init; }

    public string? Email { get; init; }

    public string? Website { get; init; }

    public string? ContactName { get; init; }
}
