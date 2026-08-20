namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>A referenced Media asset. An identifier and a role — never a URL. ADR-005.</summary>
public sealed record MediaReferenceResponse
{
    public required Guid MediaId { get; init; }

    public required string Role { get; init; }

    public required int Order { get; init; }

    public string? Caption { get; init; }
}
