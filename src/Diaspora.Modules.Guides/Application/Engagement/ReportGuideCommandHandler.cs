using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.SharedKernel.Results;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Engagement;

/// <summary>
/// Files a report against a guide, through Community.
/// <para>
/// Nothing is written in this module — the guide is read only to confirm it exists and the
/// reporter can see it, so a report cannot be used to probe for hidden or deleted content.
/// The guide is not flagged here either: what a report warrants is decided on the guide's Trust
/// moderation case, and carried out by <c>GuideModerationTarget</c>.
/// </para>
/// </summary>
public sealed class ReportGuideCommandHandler : ICommandHandler<ReportGuideCommand>
{
    private readonly IGuideRepository _guides;
    private readonly GuideReportService _reports;
    private readonly GuideAuthorizationService _authorization;

    public ReportGuideCommandHandler(
        IGuideRepository guides,
        GuideReportService reports,
        GuideAuthorizationService authorization)
    {
        _guides = guides;
        _reports = reports;
        _authorization = authorization;
    }

    public async Task<Result> Handle(
        ReportGuideCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        var guideId = GuideId.From(command.GuideId);
        var guide = await _guides.GetForReadAsync(guideId, cancellationToken);

        if (guide is null || !_authorization.CanSee(guide))
        {
            return Result.Failure(GuideErrors.NotFound);
        }

        return await _reports.ReportAsync(
            guideId,
            caller.Value,
            command.Request.Reason,
            command.Request.Description,
            cancellationToken);
    }
}
