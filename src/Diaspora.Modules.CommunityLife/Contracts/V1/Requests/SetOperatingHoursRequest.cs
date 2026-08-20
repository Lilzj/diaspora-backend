namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>Replace an organization's opening hours wholesale.</summary>
public sealed record SetOperatingHoursRequest
{
    public int? BaseRevision { get; init; }

    public IReadOnlyList<OperatingHourRequest> Hours { get; init; } = [];
}
