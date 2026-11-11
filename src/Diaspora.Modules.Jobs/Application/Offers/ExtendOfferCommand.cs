using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Contracts.V1.Requests;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;

namespace Diaspora.Modules.Jobs.Application.Offers;

/// <summary>Drafts an offer, and extends it to the candidate unless asked not to.</summary>
[RequirePermission(JobPermissions.OffersExtend)]
[Auditable]
[Transactional(Reason = "Writes the offer and moves the application it belongs to.")]
public sealed record ExtendOfferCommand : ICommand<JobOfferResponse>
{
    public required Guid ApplicationId { get; init; }

    public required ExtendOfferRequest Request { get; init; }
}
