namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>A kind of organization, as the picker renders one.</summary>
public sealed record OrganizationTypeResponse
{
    public required string Key { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? IconName { get; init; }

    public string? GroupName { get; init; }

    public required int Order { get; init; }

    /// <summary>A hint the client renders as a prompt. Never a rule that blocks publication.</summary>
    public required bool EncouragesVerification { get; init; }

    public required int OrganizationCount { get; init; }
}
