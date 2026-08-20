namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>Everything a provider says about itself in prose.</summary>
public sealed record ProviderProfileResponse
{
    /// <summary>The full text. The client's "About" section.</summary>
    public required string Description { get; init; }

    /// <summary>A card-sized precis. Derived when the provider supplied none.</summary>
    public required string Summary { get; init; }

    /// <summary>One line under the name.</summary>
    public string? Tagline { get; init; }

    /// <summary>The client's <c>CommunityOrganization.mission</c>.</summary>
    public string? Mission { get; init; }

    /// <summary>Renders as "Serving the community since 2014".</summary>
    public DateOnly? FoundedOn { get; init; }

    /// <summary>Null until enough contacts have been observed to say anything honest.</summary>
    public int? TypicalResponseHours { get; init; }
}
