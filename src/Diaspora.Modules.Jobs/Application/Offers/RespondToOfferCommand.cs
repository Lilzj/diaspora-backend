using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Contracts.V1.Requests;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;

namespace Diaspora.Modules.Jobs.Application.Offers;

/// <summary>
/// The candidate accepts or declines an offer.
/// <para>
/// Granted under restriction, deliberately: an offer is time-limited and life-changing, and a
/// restriction must not cost somebody a job by running out the clock.
/// </para>
/// <para>
/// <c>[Transactional]</c>: accepting moves the application, fills a vacancy on the posting and
/// updates the employer's acceptance rate. A hire recorded in only some of those places would be
/// wrong everywhere it was not.
/// </para>
/// </summary>
[RequirePermission(JobPermissions.OffersRespond)]
[Auditable]
[Transactional(Reason = "Answering an offer moves the application, the posting and the employer.")]
public sealed record RespondToOfferCommand : ICommand<JobOfferResponse>
{
    public required Guid OfferId { get; init; }

    public required RespondToOfferRequest Request { get; init; }
}
