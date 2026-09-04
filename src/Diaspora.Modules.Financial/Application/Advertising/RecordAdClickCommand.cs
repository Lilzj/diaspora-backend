using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Advertising;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Advertising;

/// <summary>
/// Somebody opened a promoted subject from where it was promoted — MON-GAP-002.
/// <para>
/// <b>Reported by the device, because only the device knows a tap.</b> That makes it the one metric
/// here the platform does not observe for itself, and the design says so rather than pretending
/// otherwise: the impression is server-side and authoritative, the click is client-reported and
/// checked.
/// </para>
/// <para>
/// <b>Three checks, and each closes something a device could otherwise assert.</b> The campaign has
/// to exist; it has to be <em>delivering</em>, so a click cannot be recorded against a campaign the
/// advertiser paused or one that finished last month; and the caller has to be signed in, so an
/// anonymous flood cannot inflate somebody's numbers. What none of this can stop is a signed-in
/// member clicking honestly a hundred times, which is a rate-limiting problem rather than a
/// contract one.
/// </para>
/// <para>
/// <b>Anybody may report a click, including somebody who does not own the campaign.</b> That is the
/// point — the advertiser is not the one clicking. It is why the campaign is loaded without an
/// ownership check here, and why nothing about the campaign comes back in the response.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.PurchasesRead)]
public sealed record RecordAdClickCommand : ICommand
{
    public required Guid CampaignId { get; init; }

    /// <summary>Where it was clicked from. B14's surface name, echoed back by the client.</summary>
    public string? Surface { get; init; }
}

public sealed class RecordAdClickCommandHandler : ICommandHandler<RecordAdClickCommand>
{
    private readonly IAdCampaignRepository _campaigns;
    private readonly AdPerformanceRecorder _recorder;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public RecordAdClickCommandHandler(
        IAdCampaignRepository campaigns,
        AdPerformanceRecorder recorder,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _campaigns = campaigns;
        _recorder = recorder;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        RecordAdClickCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var campaign = await _campaigns.FindAsync(
            AdCampaignId.From(command.CampaignId), cancellationToken);

        if (campaign is null)
        {
            return Result.Failure(AdCampaignErrors.NotFound);
        }

        // A click against a campaign that is not running is a number the advertiser would be shown
        // for delivery they did not buy.
        if (!campaign.IsDelivering)
        {
            return Result.Failure(AdCampaignErrors.NotDelivering);
        }

        await _recorder.RecordClickAsync(
            campaign, placementId: null, command.Surface, _clock.UtcNow, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
