namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>One row of an organization's opening hours.</summary>
public sealed record OperatingHourResponse
{
    public required Guid Id { get; init; }

    public required string Day { get; init; }

    /// <summary>Local time, <c>HH:mm</c>. Null on a closed row or a by-arrangement one.</summary>
    public string? OpensAt { get; init; }

    public string? ClosesAt { get; init; }

    public required bool IsClosed { get; init; }

    public string? Note { get; init; }
}
