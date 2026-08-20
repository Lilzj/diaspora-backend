using Diaspora.Modules.Financial.Domain.Enums;

namespace Diaspora.Modules.Financial.Extensibility.Catalog;

/// <summary>One community fund a module wants to exist at startup.</summary>
public sealed record ContributionFundSeed
{
    public required string Code { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string Currency { get; init; }

    public string? CountryCode { get; init; }

    public ContributionUrgency Urgency { get; init; } = ContributionUrgency.Ongoing;

    /// <summary>What the fund will take. All five kinds when omitted.</summary>
    public IReadOnlyList<ContributionKind> AcceptedKinds { get; init; } = [];

    public int SortOrder { get; init; } = 100;

    public bool IsSystem { get; init; }
}
