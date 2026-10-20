using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Contracts.V1.Requests;
using Diaspora.Modules.Analytics.Contracts.V1.Responses;
using Diaspora.Modules.Analytics.Domain.Errors;
using Diaspora.Modules.Analytics.Domain.Identifiers;
using Diaspora.Modules.Analytics.Domain.Reports;
using Diaspora.Modules.Analytics.Domain.Repositories;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Analytics.Application.Reports;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// FRONTEND-PHASE15-GAP-027 — saved reports.
//
// A member's own report definitions. Every read and write names the caller as the owner, and a report
// somebody else owns answers exactly as one that does not exist: 404, so an identifier cannot be used
// to learn that a report is there.
// ════════════════════════════════════════════════════════════════════════════════════════════════

internal static class AnalyticsReports
{
    /// <summary>How many reports one member may keep. Bounded, because every list read returns all of them.</summary>
    public const int MaximumPerMember = 100;

    public const string Private = "private";

    public static AnalyticsReportResponse ToResponse(AnalyticsSavedReport report, Guid callerId) => new()
    {
        ReportId = report.Id.Value.ToString(),
        Name = report.Name,
        Description = report.Description,
        Query = AnalyticsQueryPayloads.Deserialise(report.DefinitionJson),
        Visualization = report.Visualization,
        OwnerId = report.OwnerUserId.ToString(),
        IsOwnedByCaller = report.OwnerUserId == callerId,
        IsFavorite = report.IsFavorite,
        Sharing = report.Sharing,
        CreatedAt = report.CreatedAt,
        UpdatedAt = report.UpdatedAt,
        LastRunAt = null
    };

    public static Error? CheckSharing(string? sharing) =>
        sharing is null || string.Equals(sharing.Trim(), Private, StringComparison.Ordinal)
            ? null
            : AnalyticsErrors.ReportSharingUnsupported;

    public static Error? CheckVisualization(string? visualization) =>
        visualization is not null && !AnalyticsQueryPayloads.Visualizations.Contains(visualization.Trim())
            ? AnalyticsErrors.ReportInvalid("That is not a way this platform can draw a report.")
            : null;

    public static Error? CheckName(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? AnalyticsErrors.ReportInvalid("A report needs a name.")
            : name.Trim().Length > AnalyticsSavedReport.NameMaximumLength
                ? AnalyticsErrors.ReportInvalid(
                    $"A report's name can be at most {AnalyticsSavedReport.NameMaximumLength} characters.")
                : null;

    public static Error? CheckDescription(string? description) =>
        description is not null && description.Trim().Length > AnalyticsSavedReport.DescriptionMaximumLength
            ? AnalyticsErrors.ReportInvalid(
                $"A report's description can be at most {AnalyticsSavedReport.DescriptionMaximumLength} characters.")
            : null;

    /// <summary>The stored form of a query, or the reason it cannot be kept.</summary>
    public static Result<string> Define(AnalyticsQueryPayload? query)
    {
        var (normalised, problem) = AnalyticsQueryPayloads.Normalise(query);

        if (normalised is null)
        {
            return Result.Failure<string>(AnalyticsErrors.ReportInvalid(problem!));
        }

        var json = AnalyticsQueryPayloads.Serialise(normalised);

        return AnalyticsQueryPayloads.Fits(json)
            ? Result.Success(json)
            : Result.Failure<string>(AnalyticsErrors.ReportInvalid("The query is too large to save."));
    }

    public static AnalyticsSavedReportId? ParseId(string? reportId) =>
        Guid.TryParse(reportId, out var value) && value != Guid.Empty
            ? AnalyticsSavedReportId.From(value)
            : null;
}

/// <summary>The caller's saved reports. <c>mine</c> (the default), <c>favorites</c> or <c>shared</c>.</summary>
[Authorize(Permission = AnalyticsPermissions.CreatorRead)]
public sealed record ListAnalyticsReportsQuery : IQuery<IReadOnlyList<AnalyticsReportResponse>>
{
    public string? Scope { get; init; }
}

public sealed class ListAnalyticsReportsQueryHandler
    : IQueryHandler<ListAnalyticsReportsQuery, IReadOnlyList<AnalyticsReportResponse>>
{
    private readonly AnalyticsQueryContext _context;
    private readonly IAnalyticsSavedReportRepository _reports;

    public ListAnalyticsReportsQueryHandler(
        AnalyticsQueryContext context, IAnalyticsSavedReportRepository reports)
    {
        _context = context;
        _reports = reports;
    }

    public async Task<Result<IReadOnlyList<AnalyticsReportResponse>>> Handle(
        ListAnalyticsReportsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<AnalyticsReportResponse>>(caller.Error);
        }

        var scope = query.Scope?.Trim().ToLowerInvariant() ?? "mine";

        // Nothing is shared on this deployment — every report is private — so the shared list is
        // empty because it is empty, not because sharing failed.
        if (scope == "shared")
        {
            return Result.Success<IReadOnlyList<AnalyticsReportResponse>>([]);
        }

        var reports = await _reports
            .ListOwnedAsync(
                caller.Value,
                favoritesOnly: scope == "favorites",
                AnalyticsReports.MaximumPerMember,
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success<IReadOnlyList<AnalyticsReportResponse>>(
            [.. reports.Select(report => AnalyticsReports.ToResponse(report, caller.Value))]);
    }
}

/// <summary>One of the caller's saved reports.</summary>
[Authorize(Permission = AnalyticsPermissions.CreatorRead)]
public sealed record GetAnalyticsReportQuery : IQuery<AnalyticsReportResponse>
{
    public required string ReportId { get; init; }
}

public sealed class GetAnalyticsReportQueryHandler
    : IQueryHandler<GetAnalyticsReportQuery, AnalyticsReportResponse>
{
    private readonly AnalyticsQueryContext _context;
    private readonly IAnalyticsSavedReportRepository _reports;

    public GetAnalyticsReportQueryHandler(
        AnalyticsQueryContext context, IAnalyticsSavedReportRepository reports)
    {
        _context = context;
        _reports = reports;
    }

    public async Task<Result<AnalyticsReportResponse>> Handle(
        GetAnalyticsReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<AnalyticsReportResponse>(caller.Error);
        }

        if (AnalyticsReports.ParseId(query.ReportId) is not { } id)
        {
            return Result.Failure<AnalyticsReportResponse>(AnalyticsErrors.ReportNotFound);
        }

        var report = await _reports.FindOwnedAsync(id, caller.Value, cancellationToken).ConfigureAwait(false);

        return report is null
            ? Result.Failure<AnalyticsReportResponse>(AnalyticsErrors.ReportNotFound)
            : Result.Success(AnalyticsReports.ToResponse(report, caller.Value));
    }
}

/// <summary>Saves a new report definition for the caller.</summary>
[Authorize(Permission = AnalyticsPermissions.CreatorRead)]
public sealed record CreateAnalyticsReportCommand : ICommand<AnalyticsReportResponse>
{
    public required CreateAnalyticsReportRequest Request { get; init; }
}

public sealed class CreateAnalyticsReportCommandHandler
    : ICommandHandler<CreateAnalyticsReportCommand, AnalyticsReportResponse>
{
    private readonly AnalyticsQueryContext _context;
    private readonly IAnalyticsSavedReportRepository _reports;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAnalyticsReportCommandHandler(
        AnalyticsQueryContext context, IAnalyticsSavedReportRepository reports, IUnitOfWork unitOfWork)
    {
        _context = context;
        _reports = reports;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AnalyticsReportResponse>> Handle(
        CreateAnalyticsReportCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<AnalyticsReportResponse>(caller.Error);
        }

        var request = command.Request ?? new CreateAnalyticsReportRequest();

        var problem = AnalyticsReports.CheckName(request.Name)
            ?? AnalyticsReports.CheckDescription(request.Description)
            ?? AnalyticsReports.CheckVisualization(request.Visualization)
            ?? AnalyticsReports.CheckSharing(request.Sharing);

        if (problem is not null)
        {
            return Result.Failure<AnalyticsReportResponse>(problem);
        }

        var definition = AnalyticsReports.Define(request.Query);

        if (definition.IsFailure)
        {
            return Result.Failure<AnalyticsReportResponse>(definition.Error);
        }

        var held = await _reports.CountOwnedAsync(caller.Value, cancellationToken).ConfigureAwait(false);

        if (held >= AnalyticsReports.MaximumPerMember)
        {
            return Result.Failure<AnalyticsReportResponse>(
                AnalyticsErrors.ReportLimitReached(AnalyticsReports.MaximumPerMember));
        }

        var report = AnalyticsSavedReport.Create(
            AnalyticsSavedReportId.New(),
            caller.Value,
            request.Name!,
            request.Description,
            definition.Value,
            request.Visualization?.Trim() ?? "metricCards",
            _context.Calendar.UtcNow);

        _reports.Add(report);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(AnalyticsReports.ToResponse(report, caller.Value));
    }
}

/// <summary>Changes one of the caller's reports. Fields left out of the body are left as they were.</summary>
[Authorize(Permission = AnalyticsPermissions.CreatorRead)]
public sealed record UpdateAnalyticsReportCommand : ICommand<AnalyticsReportResponse>
{
    public required string ReportId { get; init; }

    public required UpdateAnalyticsReportRequest Request { get; init; }
}

public sealed class UpdateAnalyticsReportCommandHandler
    : ICommandHandler<UpdateAnalyticsReportCommand, AnalyticsReportResponse>
{
    private readonly AnalyticsQueryContext _context;
    private readonly IAnalyticsSavedReportRepository _reports;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateAnalyticsReportCommandHandler(
        AnalyticsQueryContext context, IAnalyticsSavedReportRepository reports, IUnitOfWork unitOfWork)
    {
        _context = context;
        _reports = reports;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AnalyticsReportResponse>> Handle(
        UpdateAnalyticsReportCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<AnalyticsReportResponse>(caller.Error);
        }

        if (AnalyticsReports.ParseId(command.ReportId) is not { } id)
        {
            return Result.Failure<AnalyticsReportResponse>(AnalyticsErrors.ReportNotFound);
        }

        var report = await _reports.FindOwnedAsync(id, caller.Value, cancellationToken).ConfigureAwait(false);

        if (report is null)
        {
            return Result.Failure<AnalyticsReportResponse>(AnalyticsErrors.ReportNotFound);
        }

        var request = command.Request ?? new UpdateAnalyticsReportRequest();

        var problem = (request.Name is null ? null : AnalyticsReports.CheckName(request.Name))
            ?? AnalyticsReports.CheckDescription(request.Description)
            ?? AnalyticsReports.CheckVisualization(request.Visualization)
            ?? AnalyticsReports.CheckSharing(request.Sharing);

        if (problem is not null)
        {
            return Result.Failure<AnalyticsReportResponse>(problem);
        }

        string? definition = null;

        if (request.Query is not null)
        {
            var defined = AnalyticsReports.Define(request.Query);

            if (defined.IsFailure)
            {
                return Result.Failure<AnalyticsReportResponse>(defined.Error);
            }

            definition = defined.Value;
        }

        var now = _context.Calendar.UtcNow;

        if (request.Name is not null)
        {
            report.Rename(request.Name, now);
        }

        if (request.Description is not null)
        {
            report.Describe(request.Description, now);
        }

        if (definition is not null)
        {
            report.Redefine(definition, now);
        }

        if (request.Visualization is not null)
        {
            report.Draw(request.Visualization.Trim(), now);
        }

        if (request.IsFavorite is { } isFavorite)
        {
            report.MarkFavorite(isFavorite, now);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(AnalyticsReports.ToResponse(report, caller.Value));
    }
}

/// <summary>Deletes one of the caller's reports.</summary>
[Authorize(Permission = AnalyticsPermissions.CreatorRead)]
public sealed record DeleteAnalyticsReportCommand : ICommand<AnalyticsReportDeletedResponse>
{
    public required string ReportId { get; init; }
}

public sealed class DeleteAnalyticsReportCommandHandler
    : ICommandHandler<DeleteAnalyticsReportCommand, AnalyticsReportDeletedResponse>
{
    private readonly AnalyticsQueryContext _context;
    private readonly IAnalyticsSavedReportRepository _reports;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteAnalyticsReportCommandHandler(
        AnalyticsQueryContext context, IAnalyticsSavedReportRepository reports, IUnitOfWork unitOfWork)
    {
        _context = context;
        _reports = reports;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AnalyticsReportDeletedResponse>> Handle(
        DeleteAnalyticsReportCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<AnalyticsReportDeletedResponse>(caller.Error);
        }

        if (AnalyticsReports.ParseId(command.ReportId) is not { } id)
        {
            return Result.Failure<AnalyticsReportDeletedResponse>(AnalyticsErrors.ReportNotFound);
        }

        var report = await _reports.FindOwnedAsync(id, caller.Value, cancellationToken).ConfigureAwait(false);

        if (report is null)
        {
            return Result.Failure<AnalyticsReportDeletedResponse>(AnalyticsErrors.ReportNotFound);
        }

        _reports.Remove(report);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new AnalyticsReportDeletedResponse { ReportId = report.Id.Value.ToString() });
    }
}
