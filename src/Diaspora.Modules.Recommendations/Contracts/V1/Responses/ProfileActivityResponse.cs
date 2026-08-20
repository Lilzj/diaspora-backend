namespace Diaspora.Modules.Recommendations.Contracts.V1.Responses;

/// <summary>What a member has done with one kind of thing.</summary>
public sealed record ProfileActivityResponse
{
    public required string TargetType { get; init; }

    public required int ViewCount { get; init; }

    public required int SaveCount { get; init; }

    /// <summary>Applications, enquiries, viewing requests, joins.</summary>
    public required int ApplyCount { get; init; }

    /// <summary>Offers, reservations, purchases, hires.</summary>
    public required int TransactCount { get; init; }

    public required int DismissCount { get; init; }

    public DateTimeOffset? LastActivityAt { get; init; }
}
