using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;

namespace Diaspora.Modules.Jobs.Application.Offers;

/// <summary>
/// The offers extended against one application.
/// <para>
/// **Gap G-4, closed.** The twin of G-3, and the sharper of the two: a candidate could *accept* an
/// offer through `POST /offers/{id}/respond` and had no route on which to read its terms first. The
/// employer dashboard reported <c>outstandingOfferCount</c> with nothing to open.
/// </para>
/// <para>
/// A list rather than a single live offer, because an offer can be superseded — a first set of
/// terms rejected and a second extended — and the history is what makes a negotiation legible.
/// `IJobOfferRepository.GetLiveForApplicationAsync` already answers the "what is on the table now"
/// question; this answers "what has been on the table".
/// </para>
/// </summary>
[RequirePolicy(JobPolicies.ApplicationsReview)]
public sealed record ListApplicationOffersQuery : IQuery<IReadOnlyList<JobOfferResponse>>
{
    public required Guid ApplicationId { get; init; }
}
