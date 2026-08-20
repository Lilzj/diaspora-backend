namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>Edits the prose and imagery a provider presents itself with.</summary>
public sealed record UpdateProviderProfileRequest
{
    public string? Description { get; init; }

    /// <summary>Card-sized. Derived from the description when omitted.</summary>
    public string? Summary { get; init; }

    public string? Tagline { get; init; }

    /// <summary>The client's <c>CommunityOrganization.mission</c>.</summary>
    public string? Mission { get; init; }

    public DateOnly? FoundedOn { get; init; }

    /// <summary>A Media identifier. Never a URL.</summary>
    public Guid? CoverMediaId { get; init; }

    /// <summary>A Media identifier. Never a URL.</summary>
    public Guid? LogoMediaId { get; init; }
}
