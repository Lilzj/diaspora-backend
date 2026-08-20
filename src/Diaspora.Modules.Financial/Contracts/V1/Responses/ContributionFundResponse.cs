namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>A standing community fund.</summary>
public sealed record ContributionFundResponse
{
    public required Guid FundId { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string Urgency { get; init; }

    public required IReadOnlyList<string> AcceptedKinds { get; init; }

    public required MoneyResponse Raised { get; init; }

    public MoneyResponse? Target { get; init; }

    /// <summary>Everybody who has given anything, monetary or not.</summary>
    public required int ContributorCount { get; init; }

    public required bool IsActive { get; init; }

    public string? CountryCode { get; init; }

    public string? MediaReference { get; init; }

    public required int SortOrder { get; init; }
}
