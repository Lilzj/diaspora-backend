using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Neighborhoods;

/// <summary>A moderator approving, rejecting or retiring an insight.</summary>
[Authorize(Permission = HousingPermissions.InsightModerate)]
[Auditable]
public sealed record DecideNeighborhoodInsightCommand : ICommand<NeighborhoodInsightResponse>
{
    public required Guid InsightId { get; init; }

    public required DecideNeighborhoodInsightRequest Request { get; init; }
}
