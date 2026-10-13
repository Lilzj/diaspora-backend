using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Application.Mapping;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Moderation;

/// <summary>
/// Files a report.
/// <para>
/// The reason accepts both of the client's taxonomies, and the conflicting-information category
/// is the one that needs its link: without the thing being contradicted, a misinformation report
/// is unactionable.
/// </para>
/// </summary>
public sealed class ReportContentCommandHandler
    : ICommandHandler<ReportContentCommand, ReportResponse>
{
    private readonly ModerationService _moderation;
    private readonly ContentReferenceResolver _references;
    private readonly ICurrentUser _currentUser;

    public ReportContentCommandHandler(
        ModerationService moderation,
        ContentReferenceResolver references,
        ICurrentUser currentUser)
    {
        _moderation = moderation;
        _references = references;
        _currentUser = currentUser;
    }

    public async Task<Result<ReportResponse>> Handle(
        ReportContentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure<ReportResponse>(CommunityErrors.NotAuthenticated);
        }

        var reference = _references.Resolve(
            request.Request.ContentType, request.Request.ContentId);

        if (reference.IsFailure)
        {
            return Result.Failure<ReportResponse>(reference.Error);
        }

        // Written and published by the platform's own staff (13 Oct 2026). Hiding the button in
        // the app is not enough on its own: the route would still take a report.
        if (reference.Value.Type == CommunityContentType.Notice)
        {
            return Result.Failure<ReportResponse>(CommunityErrors.ContentNotReportable);
        }

        var reason = CommunityWireNames.ParseReportReason(request.Request.Reason);

        if (reason.IsFailure)
        {
            return Result.Failure<ReportResponse>(reason.Error);
        }

        if (reason.Value == ReportReasonCode.Misinformation
            && string.Equals(request.Request.Reason?.Trim(), "conflicting",
                StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(request.Request.EvidenceUrl))
        {
            return Result.Failure<ReportResponse>(CommunityErrors.ReportEvidenceRequired);
        }

        var filed = await _moderation.ReportAsync(
            reference.Value,
            userId,
            reason.Value,
            request.Request.Description,
            request.Request.EvidenceUrl,
            cancellationToken);

        return filed.IsFailure
            ? Result.Failure<ReportResponse>(filed.Error)
            : Result.Success(ModerationMapper.ToResponse(filed.Value));
    }
}
