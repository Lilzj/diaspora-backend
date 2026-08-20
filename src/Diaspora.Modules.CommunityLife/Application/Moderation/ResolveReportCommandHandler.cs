using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Domain.Reports;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Moderation;

/// <summary>
/// Closes a report, optionally acting on its subject at the same time.
/// <para>
/// <b>The decision and the action are one transaction.</b> A moderator who upheld a report about a
/// fraudulent page and then failed to hide it would have recorded a judgement nobody acted on, and
/// the page would still be collecting money. Dismissing a report deliberately clears nothing: a
/// subject flagged by other reports stays flagged, because this decision was about one of them.
/// </para>
/// </summary>
public sealed class ResolveReportCommandHandler
    : ICommandHandler<ResolveReportCommand, ReportResponse>
{
    private readonly ICommunityLifeReportRepository _reports;
    private readonly ICommunityEventRepository _events;
    private readonly IOrganizationRepository _organizations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly IClock _clock;

    public ResolveReportCommandHandler(
        ICommunityLifeReportRepository reports,
        ICommunityEventRepository events,
        IOrganizationRepository organizations,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        IClock clock)
    {
        _reports = reports;
        _events = events;
        _organizations = organizations;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<ReportResponse>> Handle(
        ResolveReportCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var staff = _authorization.RequireStaff();

        if (staff.IsFailure)
        {
            return Result.Failure<ReportResponse>(staff.Error);
        }

        var report = await _reports.GetAsync(
            CommunityLifeReportId.From(command.ReportId), cancellationToken);

        if (report is null)
        {
            return Result.Failure<ReportResponse>(CommunityLifeErrors.ReportNotFound);
        }

        if (CommunityLifeSharedMapper.ParseEnum<CommunityLifeReportStatus>(
                command.Request.Outcome) is not { } outcome)
        {
            return Result.Failure<ReportResponse>(CommunityLifeErrors.ReportAlreadyResolved);
        }

        var now = _clock.UtcNow;

        var resolved = report.Resolve(
            outcome,
            staff.Value,
            command.Request.Note,
            now,
            command.Request.MergedIntoSubjectId);

        if (resolved.IsFailure)
        {
            return Result.Failure<ReportResponse>(resolved.Error);
        }

        _reports.Update(report);

        var applied = await ApplyActionAsync(report, command, now, cancellationToken);

        if (applied.IsFailure)
        {
            return Result.Failure<ReportResponse>(applied.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // The moderator sees who reported it; nobody else does. That is the difference between the
        // queue and the public contract, and it is decided here rather than in the mapper's default.
        return Result.Success(CommunityLifeCatalogueMapper.ToResponse(
            report, includeReporter: true));
    }

    /// <summary>
    /// Applies the moderation action the moderator asked for alongside the decision.
    /// <para>
    /// Optional: a report can be closed without touching its subject, which is what dismissing one
    /// means. Where an action is named it is applied to the subject the report was about, loaded
    /// past the delete filter so a subject already taken down can still be acted on.
    /// </para>
    /// </summary>
    private async Task<Result> ApplyActionAsync(
        CommunityLifeReport report,
        ResolveReportCommand command,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (CommunityLifeSharedMapper.ParseEnum<CommunityLifeModerationState>(
                command.Request.Action) is not { } action
            || action == CommunityLifeModerationState.Flagged)
        {
            return Result.Success();
        }

        var moderator = _authorization.CallerId;
        var note = command.Request.Note;

        if (report.SubjectType == ReportSubjectType.Organization)
        {
            var organization = await _organizations.GetIncludingDeletedAsync(
                OrganizationId.From(report.SubjectId), cancellationToken);

            if (organization is null)
            {
                return Result.Failure(CommunityLifeErrors.OrganizationNotFound);
            }

            switch (action)
            {
                case CommunityLifeModerationState.Hidden:
                    organization.Hide(note, moderator, now);
                    break;
                case CommunityLifeModerationState.Suspended:
                    organization.Suspend(note, moderator, now);
                    break;
                default:
                    organization.Clear(moderator, now);
                    break;
            }

            _organizations.Update(organization);

            return Result.Success();
        }

        var communityEvent = await _events.GetIncludingDeletedAsync(
            CommunityEventId.From(report.SubjectId), cancellationToken);

        if (communityEvent is null)
        {
            return Result.Failure(CommunityLifeErrors.EventNotFound);
        }

        switch (action)
        {
            case CommunityLifeModerationState.Hidden:
                communityEvent.Hide(note, moderator, now);
                break;
            case CommunityLifeModerationState.Suspended:
                communityEvent.Suspend(note, moderator, now);
                break;
            default:
                communityEvent.Clear(moderator, now);
                break;
        }

        _events.Update(communityEvent);

        return Result.Success();
    }
}
