using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Neighborhoods;

/// <summary>
/// Agreeing, or withdrawing agreement, with a published insight.
/// <para>
/// Endorsement is what turns one member opinion into something a profile can rank by: one person
/// saying the water goes off is an anecdote, eleven people saying it is a fact about the place.
/// </para>
/// </summary>
[Authorize(Permission = HousingPermissions.InsightEndorse)]
[Transactional(Reason = "Writes the endorsement row and the insight count together.")]
[Auditable]
public sealed record EndorseNeighborhoodInsightCommand : ICommand<NeighborhoodInsightResponse>
{
    public required Guid InsightId { get; init; }

    public required EndorseInsightRequest Request { get; init; }
}
