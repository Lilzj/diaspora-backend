using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Intelligence.Services;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Extensibility.Analytics;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Intelligence.Notices;

/// <summary>
/// Records one thing a reader did with a notice.
/// <para>
/// <b>Two counters move: the notice's own and the country's.</b> They answer different questions —
/// "which notice was read" ranks a most-viewed list, "how much was this country's intelligence used"
/// is what an editorial team reads — and neither is derivable from the other without a scan.
/// </para>
/// <para>
/// <b>Neither increment bumps the notice's revision or raises an event</b>, which is B8's rule
/// verbatim: engagement is not an edit. A notice being read by ten thousand people during a border
/// closure must not give the editor trying to update it ten thousand concurrency conflicts, nor fill
/// the outbox with ten thousand messages.
/// </para>
/// <para>
/// <b>An unknown activity is ignored rather than refused.</b> A client sending a kind a later
/// deployment removed should not see an error on a fire-and-forget call it makes after every read;
/// the signal is simply not counted.
/// </para>
/// </summary>
public sealed class RecordNoticeActivityCommandHandler
    : ICommandHandler<RecordNoticeActivityCommand>
{
    private readonly IOfficialNoticeRepository _notices;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IntelligenceAnalyticsRecorder _analytics;

    public RecordNoticeActivityCommandHandler(
        IOfficialNoticeRepository notices,
        IUnitOfWork unitOfWork,
        IntelligenceAnalyticsRecorder analytics)
    {
        _notices = notices;
        _unitOfWork = unitOfWork;
        _analytics = analytics;
    }

    public async Task<Result> Handle(
        RecordNoticeActivityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var notice = await _notices.GetAsync(
            OfficialNoticeId.From(command.OfficialNoticeId), cancellationToken);

        if (notice is null)
        {
            return Result.Failure(IntelligenceErrors.NoticeNotFound);
        }

        // A reader may only ever record activity against something they could have read. Without
        // this, an unauthenticated caller could count views on a draft and discover its existence
        // from the difference between a 404 and a 204.
        if (!notice.IsReaderVisible)
        {
            return Result.Failure(IntelligenceErrors.NoticeNotFound);
        }

        var kind = Resolve(command.Request.Activity);

        if (kind is null)
        {
            return Result.Success();
        }

        Apply(notice, kind);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _analytics.RecordAsync(
            _analytics.Signal(
                kind,
                notice.CountryCode,
                notice.Id.Value,
                notice.CategoryKey,
                guideId: command.Request.GuideId,
                source: command.Request.Source,
                locale: command.Request.Locale),
            cancellationToken);

        return Result.Success();
    }

    private static string? Resolve(string? activity) =>
        activity?.Trim().ToLowerInvariant() switch
        {
            "view" => IntelligenceAnalyticsSignalKinds.NoticeViewed,
            "click-through" => IntelligenceAnalyticsSignalKinds.NoticeClickThrough,
            "completion" => IntelligenceAnalyticsSignalKinds.NoticeCompleted,
            "guide" => IntelligenceAnalyticsSignalKinds.GuideCrossNavigation,
            _ => null
        };

    /// <summary>
    /// The aggregate's own counters, for the three activities it tracks.
    /// <para>
    /// Cross-navigation is deliberately absent: "how many readers left this notice for a guide" is a
    /// question about the pair, and putting it on the notice would make it look like an engagement
    /// figure comparable with the other three when it is not.
    /// </para>
    /// </summary>
    private static void Apply(OfficialNotice notice, string kind)
    {
        switch (kind)
        {
            case IntelligenceAnalyticsSignalKinds.NoticeViewed:
                notice.RecordView();
                break;

            case IntelligenceAnalyticsSignalKinds.NoticeClickThrough:
                notice.RecordClickThrough();
                break;

            case IntelligenceAnalyticsSignalKinds.NoticeCompleted:
                notice.RecordCompletion();
                break;

            default:
                break;
        }
    }
}
