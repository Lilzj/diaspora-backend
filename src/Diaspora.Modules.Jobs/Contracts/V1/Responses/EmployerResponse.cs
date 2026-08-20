namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>One employer profile in full.</summary>
public sealed record EmployerResponse
{
    public required Guid EmployerId { get; init; }

    public required string Slug { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>The registered entity name. Present to the team and to staff only.</summary>
    public string? LegalName { get; init; }

    public string? RegistrationNumber { get; init; }

    public required string OrganizationType { get; init; }

    public required bool IsIntermediary { get; init; }

    public required string CompanySize { get; init; }

    public int? FoundedYear { get; init; }

    public required string CountryCode { get; init; }

    public string? Description { get; init; }

    public string? Tagline { get; init; }

    public Guid? LogoMediaId { get; init; }

    public Guid? CoverMediaId { get; init; }

    public string? ContactEmail { get; init; }

    public string? ContactPhone { get; init; }

    public string? WebsiteUrl { get; init; }

    public string? CareersUrl { get; init; }

    public required string Status { get; init; }

    public required bool IsVerified { get; init; }

    public string? VerificationState { get; init; }

    public string? VerificationBadge { get; init; }

    public required IReadOnlyList<HiringRegionResponse> HiringRegions { get; init; }

    /// <summary>The team. Present to the team and to staff only — never to candidates.</summary>
    public IReadOnlyList<EmployerMemberResponse>? Members { get; init; }

    public required EmployerResponsivenessResponse Responsiveness { get; init; }

    public required int OpenJobCount { get; init; }

    public required DateTimeOffset RegisteredAt { get; init; }

    public required int Revision { get; init; }

    /// <summary>This viewer's role here, when they belong. Null otherwise.</summary>
    public string? ViewerRole { get; init; }
}
