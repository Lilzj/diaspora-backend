using System.Globalization;
using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Contracts.V1.Requests;
using Diaspora.Modules.Analytics.Contracts.V1.Responses;
using Diaspora.Modules.Analytics.Domain.Errors;
using Diaspora.Modules.Analytics.Domain.Identifiers;
using Diaspora.Modules.Analytics.Domain.Projection;
using Diaspora.Modules.Analytics.Domain.Repositories;
using Diaspora.Modules.Analytics.Options;
using Diaspora.SharedKernel.Results;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Analytics.Application.Operations;

/// <summary>
/// Requests a bounded rebuild of the rollups.
/// <para>
/// <b>Bounded, one at a time, and administrative.</b> §30 forbids "a dangerous rebuild-all endpoint
/// exposed to normal users", and each of those three words is a rule here: the window is capped by
/// configuration and refused above it rather than truncated; a second request while one is running
/// is a conflict rather than a second run competing for the same cells; and the permission is
/// role-gated to administrators.
/// </para>
/// <para>
/// <b>It queues rather than runs.</b> A rebuild of ninety days inside a request would hold a
/// connection for minutes and time out at whatever proxy is in front of it — and the retry would
/// start a second one. The run is a record; the worker advances it a slice at a time and it survives
/// a restart.
/// </para>
/// </summary>
[Authorize(Permission = AnalyticsPermissions.OperationsRun)]
[Auditable(Action = "analytics.backfill.request")]
public sealed record StartAnalyticsBackfillCommand : ICommand<AnalyticsBackfillResponse>
{
    public required AnalyticsBackfillRequest Request { get; init; }
}

public sealed class StartAnalyticsBackfillCommandValidator
    : AbstractValidator<StartAnalyticsBackfillCommand>
{
    public StartAnalyticsBackfillCommandValidator() =>
        RuleFor(command => command.Request).NotNull();
}

public sealed class StartAnalyticsBackfillCommandHandler
    : ICommandHandler<StartAnalyticsBackfillCommand, AnalyticsBackfillResponse>
{
    private readonly IAnalyticsProjectionRepository _runs;
    private readonly AnalyticsQueryContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AnalyticsOptions _options;

    public StartAnalyticsBackfillCommandHandler(
        IAnalyticsProjectionRepository runs,
        AnalyticsQueryContext context,
        IUnitOfWork unitOfWork,
        IOptions<AnalyticsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _runs = runs;
        _context = context;
        _unitOfWork = unitOfWork;
        _options = options.Value;
    }

    public async Task<Result<AnalyticsBackfillResponse>> Handle(
        StartAnalyticsBackfillCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<AnalyticsBackfillResponse>(caller.Error);
        }

        if (!TryParseDay(command.Request.From, out var from)
            || !TryParseDay(command.Request.To, out var to))
        {
            return Result.Failure<AnalyticsBackfillResponse>(AnalyticsErrors.RangeIncomplete);
        }

        if (from > to)
        {
            return Result.Failure<AnalyticsBackfillResponse>(AnalyticsErrors.RangeInverted);
        }

        if (to.DayNumber - from.DayNumber + 1 > _options.MaximumBackfillDays)
        {
            return Result.Failure<AnalyticsBackfillResponse>(
                AnalyticsErrors.BackfillWindowTooWide(_options.MaximumBackfillDays));
        }

        var active = await _runs.FindActiveRunAsync(cancellationToken).ConfigureAwait(false);

        if (active is not null)
        {
            return Result.Failure<AnalyticsBackfillResponse>(AnalyticsErrors.BackfillInFlight);
        }

        var run = AnalyticsBackfillRun.Request(
            AnalyticsBackfillRunId.New(), from, to, caller.Value, _context.Calendar.UtcNow);

        _runs.Add(run);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(ToResponse(run));
    }

    private static bool TryParseDay(string? value, out DateOnly day) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out day);

    internal static AnalyticsBackfillResponse ToResponse(AnalyticsBackfillRun run) => new()
    {
        RunId = run.Id.Value.ToString(),
        Status = run.Status.ToString(),
        FromDay = run.FromDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ToDay = run.ToDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        NextDay = run.NextDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        CellsRebuilt = run.CellsRebuilt,
        RequestedAt = run.RequestedAt,
        StartedAt = run.StartedAt,
        CompletedAt = run.CompletedAt,
        FailureReason = run.FailureReason
    };
}

/// <summary>Reads a backfill run, so an operator can watch it without reading logs.</summary>
[Authorize(Permission = AnalyticsPermissions.OperationsRun)]
public sealed record GetAnalyticsBackfillQuery : IQuery<AnalyticsBackfillResponse>
{
    public required Guid RunId { get; init; }
}

public sealed class GetAnalyticsBackfillQueryHandler
    : IQueryHandler<GetAnalyticsBackfillQuery, AnalyticsBackfillResponse>
{
    private readonly IAnalyticsProjectionRepository _runs;

    public GetAnalyticsBackfillQueryHandler(IAnalyticsProjectionRepository runs) => _runs = runs;

    public async Task<Result<AnalyticsBackfillResponse>> Handle(
        GetAnalyticsBackfillQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var run = await _runs
            .FindRunAsync(AnalyticsBackfillRunId.From(query.RunId), cancellationToken)
            .ConfigureAwait(false);

        return run is null
            ? Result.Failure<AnalyticsBackfillResponse>(AnalyticsErrors.SubjectNotFound)
            : Result.Success(StartAnalyticsBackfillCommandHandler.ToResponse(run));
    }
}
