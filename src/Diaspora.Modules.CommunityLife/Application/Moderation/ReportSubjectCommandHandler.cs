using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Analytics;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Domain.Reports;
using Diaspora.Modules.CommunityLife.Observability;
using Diaspora.Modules.CommunityLife.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.CommunityLife.Application.Moderation;

/// <summary>
/// Reports an event or an organization.
/// <para>
/// <b>One report per person per subject.</b> Without it, a co-ordinated few could flag anything off
/// the platform by reporting it repeatedly — and the threshold that auto-hides is deliberately low,
/// because the reports that matter most arrive in ones and twos.
/// </para>
/// <para>
/// <b>Impersonation and fraud flag on the first report.</b> A page pretending to be a real church
/// collecting real money does damage in hours, and waiting for a third complaint is waiting too
/// long. The aggregate applies the threshold; this handler decides which threshold to hand it.
/// </para>
/// <para>
/// <b>Reporting your own page is refused.</b> Not because it is harmful, but because it is always a
/// mistake, and letting it through would put noise in the one queue that has to stay readable.
/// </para>
/// </summary>
public sealed class ReportSubjectCommandHandler
    : ICommandHandler<ReportSubjectCommand, ReportResponse>
{
    private readonly ICommunityLifeReportRepository _reports;
    private readonly ICommunityEventRepository _events;
    private readonly IOrganizationRepository _organizations;
    private readonly IEventAnalyticsRepository _eventAnalytics;
    private readonly IOrganizationAnalyticsRepository _organizationAnalytics;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly IOptions<CommunityLifeOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public ReportSubjectCommandHandler(
        ICommunityLifeReportRepository reports,
        ICommunityEventRepository events,
        IOrganizationRepository organizations,
        IEventAnalyticsRepository eventAnalytics,
        IOrganizationAnalyticsRepository organizationAnalytics,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        IOptions<CommunityLifeOptions> options,
        IGuidGenerator guids,
        IClock clock)
    {
        _reports = reports;
        _events = events;
        _organizations = organizations;
        _eventAnalytics = eventAnalytics;
        _organizationAnalytics = organizationAnalytics;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<ReportResponse>> Handle(
        ReportSubjectCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<ReportResponse>(caller.Error);
        }

        if (CommunityLifeSharedMapper.ParseEnum<ReportSubjectType>(command.SubjectType)
            is not { } subjectType)
        {
            return Result.Failure<ReportResponse>(CommunityLifeErrors.ReportSubjectInvalid);
        }

        if (CommunityLifeSharedMapper.ParseEnum<CommunityLifeReportReason>(
                command.Request.Reason) is not { } reason)
        {
            return Result.Failure<ReportResponse>(CommunityLifeErrors.ReportReasonInvalid);
        }

        var owner = await ResolveOwnerAsync(subjectType, command.SubjectId, cancellationToken);

        if (owner.IsFailure)
        {
            return Result.Failure<ReportResponse>(owner.Error);
        }

        if (owner.Value == caller.Value)
        {
            return Result.Failure<ReportResponse>(CommunityLifeErrors.CannotReportOwn);
        }

        if (await _reports.HasReportedAsync(
                subjectType, command.SubjectId, caller.Value, cancellationToken))
        {
            return Result.Failure<ReportResponse>(CommunityLifeErrors.AlreadyReported);
        }

        var now = _clock.UtcNow;

        var report = CommunityLifeReport.File(
            CommunityLifeReportId.From(_guids.NewSequentialGuid()),
            subjectType,
            command.SubjectId,
            caller.Value,
            reason,
            command.Request.Detail,
            now);

        if (report.IsFailure)
        {
            return Result.Failure<ReportResponse>(report.Error);
        }

        await _reports.AddAsync(report.Value, cancellationToken);

        await RecordAgainstSubjectAsync(report.Value, caller.Value, now, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        CommunityLifeTelemetry.SubjectsReported.Add(
            1,
            new KeyValuePair<string, object?>("subject", subjectType.ToString()),
            new KeyValuePair<string, object?>("reason", reason.ToString()));

        return Result.Success(CommunityLifeCatalogueMapper.ToResponse(
            report.Value, includeReporter: false));
    }

    /// <summary>Who owns the subject, and whether it exists at all.</summary>
    private async Task<Result<UserId>> ResolveOwnerAsync(
        ReportSubjectType subjectType, Guid subjectId, CancellationToken cancellationToken)
    {
        if (subjectType == ReportSubjectType.Organization)
        {
            var organization = await _organizations.GetAsync(
                OrganizationId.From(subjectId), cancellationToken);

            return organization is null
                ? Result.Failure<UserId>(CommunityLifeErrors.OrganizationNotFound)
                : Result.Success(organization.CreatedByUserId);
        }

        var communityEvent = await _events.GetAsync(
            CommunityEventId.From(subjectId), cancellationToken);

        return communityEvent is null
            ? Result.Failure<UserId>(CommunityLifeErrors.EventNotFound)
            : Result.Success(communityEvent.OrganizerUserId);
    }

    /// <summary>
    /// Moves the subject's own report count, and lets it decide whether to flag itself.
    /// <para>
    /// The threshold comes from configuration, which the domain refuses to know about — so the
    /// aggregate is handed the number, and a reason that should flag on sight is handed a one.
    /// </para>
    /// </summary>
    private async Task RecordAgainstSubjectAsync(
        CommunityLifeReport report,
        UserId reportedBy,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var threshold = report.FlagsImmediately
            ? 1
            : _options.Value.ReportsBeforeAutoFlag;

        if (report.SubjectType == ReportSubjectType.Organization)
        {
            var organization = await _organizations.GetAsync(
                OrganizationId.From(report.SubjectId), cancellationToken);

            organization?.RecordReport(threshold, now, reportedBy);

            var analytics = await _organizationAnalytics.FindByOrganizationAsync(
                OrganizationId.From(report.SubjectId), cancellationToken);

            analytics?.RecordReport(now);

            return;
        }

        var communityEvent = await _events.GetAsync(
            CommunityEventId.From(report.SubjectId), cancellationToken);

        communityEvent?.RecordReport(threshold, now, reportedBy);

        var eventAnalytics = await _eventAnalytics.FindByEventAsync(
            CommunityEventId.From(report.SubjectId), cancellationToken);

        eventAnalytics?.RecordReport(now);
    }
}
