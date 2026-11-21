using Diaspora.Modules.Financial.Contracts.V1.Requests;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Integration;
using Diaspora.SharedKernel.Results;
using MediatR;

namespace Diaspora.Modules.Financial.Application.Campaigns;

/// <summary>
/// Constructs B15's own campaign envelopes on B15's side of the seam.
/// <para>
/// Deliberately thin, and that is the whole point: the work was always B15's, and the only thing
/// that moves here is which assembly writes <c>new CreateCampaignCommand</c>. Dispatching through
/// <see cref="ISender"/> rather than calling the handlers keeps every pipeline behaviour —
/// authorisation, validation, auditing, the transaction — exactly where it was, which is why a
/// campaign created through B18's route is indistinguishable from one created any other way.
/// </para>
/// <para>
/// <b>No behaviour lives here and none should.</b> A guard added in this class would be a rule B15
/// enforces for one caller and not for itself. Where the previous arrangement needed a decision —
/// the five-page read behind <c>/campaigns/me</c> — the decision stays with the route that wanted
/// it, because it is a shape of that route's answer rather than a rule about campaigns.
/// </para>
/// </summary>
public sealed class Campaigns : ICampaigns
{
    private readonly ISender _sender;

    public Campaigns(ISender sender) => _sender = sender;

    public Task<Result<CampaignResponse>> CreateAsync(
        CreateCampaignRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _sender.Send(new CreateCampaignCommand { Request = request }, cancellationToken);
    }

    public Task<Result<GivingPageResponse<CampaignResponse>>> SearchAsync(
        CampaignSearch search, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(search);

        return _sender.Send(
            new SearchCampaignsQuery
            {
                State = search.State,
                Category = search.Category,
                CountryCode = search.CountryCode,
                OrganizationId = search.OrganizationId,
                MineOnly = search.MineOnly,
                Cursor = search.Cursor,
                Take = search.Take
            },
            cancellationToken);
    }

    public Task<Result<CampaignResponse>> GetAsync(
        Guid campaignId, CancellationToken cancellationToken = default) =>
        _sender.Send(new GetCampaignQuery { CampaignId = campaignId }, cancellationToken);

    public Task<Result<CampaignResponse>> UpdateAsync(
        Guid campaignId, UpdateCampaignRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _sender.Send(
            new UpdateCampaignCommand { CampaignId = campaignId, Request = request },
            cancellationToken);
    }

    public Task<Result<CampaignResponse>> TransitionAsync(
        Guid campaignId, string target, CampaignTransitionRequest? request,
        CancellationToken cancellationToken = default) =>
        _sender.Send(
            new TransitionCampaignCommand
            {
                CampaignId = campaignId,
                Target = target,
                Request = request
            },
            cancellationToken);

    public Task<Result<GivingPageResponse<CampaignUpdateResponse>>> GetUpdatesAsync(
        Guid campaignId, string? cursor, int? take,
        CancellationToken cancellationToken = default) =>
        _sender.Send(
            new GetCampaignUpdatesQuery { CampaignId = campaignId, Cursor = cursor, Take = take },
            cancellationToken);

    public Task<Result<CampaignUpdateResponse>> PublishUpdateAsync(
        Guid campaignId, CreateCampaignUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _sender.Send(
            new PublishCampaignUpdateCommand { CampaignId = campaignId, Request = request },
            cancellationToken);
    }

    public Task<Result<IReadOnlyList<CampaignMilestoneResponse>>> GetMilestonesAsync(
        Guid campaignId, CancellationToken cancellationToken = default) =>
        _sender.Send(
            new GetCampaignMilestonesQuery { CampaignId = campaignId }, cancellationToken);

    public Task<Result<IReadOnlyList<CampaignMilestoneResponse>>> SetMilestonesAsync(
        Guid campaignId, SetCampaignMilestonesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _sender.Send(
            new SetCampaignMilestonesCommand { CampaignId = campaignId, Request = request },
            cancellationToken);
    }

    public Task<Result<GivingPageResponse<CampaignSupporterResponse>>> GetSupportersAsync(
        Guid campaignId, string? cursor, int? take,
        CancellationToken cancellationToken = default) =>
        _sender.Send(
            new GetCampaignSupportersQuery
            {
                CampaignId = campaignId,
                Cursor = cursor,
                Take = take
            },
            cancellationToken);

    public Task<Result<IReadOnlyList<CampaignDonationResponse>>> GetMyDonationsAsync(
        Guid campaignId, int? take, CancellationToken cancellationToken = default) =>
        _sender.Send(
            new GetMyCampaignDonationsQuery { CampaignId = campaignId, Take = take },
            cancellationToken);

    public Task<Result<CampaignFundUsageResponse>> GetFundUsageAsync(
        Guid campaignId, CancellationToken cancellationToken = default) =>
        _sender.Send(
            new GetCampaignFundUsageQuery { CampaignId = campaignId }, cancellationToken);

    public Task<Result<CampaignFundUsageResponse>> SetFundUsageAsync(
        Guid campaignId, SetCampaignFundUsageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _sender.Send(
            new SetCampaignFundUsageCommand { CampaignId = campaignId, Request = request },
            cancellationToken);
    }
}
