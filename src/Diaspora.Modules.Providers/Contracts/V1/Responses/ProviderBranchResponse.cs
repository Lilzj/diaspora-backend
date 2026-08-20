namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>
/// One physical place a provider operates from, beyond its principal one.
/// <para>
/// Named "branch" on the wire rather than "location", because
/// <c>ProviderLocationResponse</c> already means "where the provider is" and two types called
/// location in one payload is how a client ends up rendering the wrong one.
/// </para>
/// </summary>
public sealed record ProviderBranchResponse
{
    public required Guid BranchId { get; init; }

    /// <summary>"Peckham branch", "Main hall", "Ebène office".</summary>
    public required string Name { get; init; }

    public required ProviderLocationResponse Location { get; init; }

    public required bool IsPrimary { get; init; }

    /// <summary>Hours in prose, for a branch whose schedule differs from the provider's.</summary>
    public string? OperatingHoursNote { get; init; }

    public string? PhoneNumber { get; init; }
}
