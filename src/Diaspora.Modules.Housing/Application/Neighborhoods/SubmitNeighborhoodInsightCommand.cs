using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Neighborhoods;

/// <summary>
/// Shares one thing a member knows about a place.
/// <para>
/// Limited tier, because the thing that makes neighbourhood insights worth having is that ordinary
/// residents write them. Everything they write is moderated before anybody sees it.
/// </para>
/// </summary>
[Authorize(Permission = HousingPermissions.InsightSubmit)]
[Auditable]
public sealed record SubmitNeighborhoodInsightCommand : ICommand<NeighborhoodInsightResponse>
{
    public required SubmitNeighborhoodInsightRequest Request { get; init; }
}
