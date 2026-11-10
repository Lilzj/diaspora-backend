using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Contracts.V1.Requests;
using Diaspora.Modules.Analytics.Contracts.V1.Responses;
using Diaspora.Modules.Analytics.Domain.Errors;
using Diaspora.Modules.Analytics.Domain.Exports;
using Diaspora.Modules.Analytics.Domain.Identifiers;
using Diaspora.Modules.Analytics.Domain.Repositories;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Analytics.Application.Exports;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// FRONTEND-PHASE15-GAP-026 — exports.
//
// The client requests a job and polls it; it never assembles a file itself. A dataset is small — a
// headline set and a few hundred dated points — so the job is produced while the request waits and
// is `ready` on the first poll. What the client polls for is the link, and the link is short-lived.
// ════════════════════════════════════════════════════════════════════════════════════════════════

internal static class AnalyticsExports
{
    /// <summary>How long a download link answers after the file is produced.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

    public const string Csv = "csv";

    public static AnalyticsExportResponse ToResponse(AnalyticsExport export, DateTimeOffset now)
    {
        var expired = export.HasExpired(now);

        return new AnalyticsExportResponse
        {
            ExportId = export.Id.Value.ToString(),
            Format = export.Format,
            Status = expired
                ? "expired"
                : export.Status switch
                {
                    AnalyticsExportStatus.Ready => "ready",
                    AnalyticsExportStatus.Failed => "failed",
                    _ => "queued"
                },
            Query = AnalyticsQueryPayloads.Deserialise(export.DefinitionJson),
            RequestedAt = export.RequestedAt,
            CompletedAt = export.CompletedAt,
            DownloadUrl = export.Status == AnalyticsExportStatus.Ready && !expired && export.DownloadToken is { } token
                ? DownloadPath(export.Id.Value, token)
                : null,
            SizeBytes = export.SizeBytes,
            FailureReason = export.FailureReason,
            ExpiresAt = export.ExpiresAt
        };
    }

    public static string DownloadPath(Guid exportId, string token) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{AnalyticsModuleMetadata.AnalyticsRoutePrefix}/exports/{exportId}/download?token={Uri.EscapeDataString(token)}");

    /// <summary>A random, URL-safe capability for one export's link. 256 bits.</summary>
    public static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    public static AnalyticsExportId? ParseId(string? exportId) =>
        Guid.TryParse(exportId, out var value) && value != Guid.Empty
            ? AnalyticsExportId.From(value)
            : null;
}

/// <summary>Produces an export of one subject's dataset for the caller.</summary>
[Authorize(Permission = AnalyticsPermissions.CreatorRead)]
public sealed record RequestAnalyticsExportCommand : ICommand<AnalyticsExportResponse>
{
    public required RequestAnalyticsExportRequest Request { get; init; }
}

public sealed class RequestAnalyticsExportCommandHandler
    : ICommandHandler<RequestAnalyticsExportCommand, AnalyticsExportResponse>
{
    private readonly AnalyticsQueryContext _context;
    private readonly AnalyticsSubjectDatasets _datasets;
    private readonly IAnalyticsExportRepository _exports;
    private readonly IUnitOfWork _unitOfWork;

    public RequestAnalyticsExportCommandHandler(
        AnalyticsQueryContext context,
        AnalyticsSubjectDatasets datasets,
        IAnalyticsExportRepository exports,
        IUnitOfWork unitOfWork)
    {
        _context = context;
        _datasets = datasets;
        _exports = exports;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AnalyticsExportResponse>> Handle(
        RequestAnalyticsExportCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<AnalyticsExportResponse>(caller.Error);
        }

        var request = command.Request ?? new RequestAnalyticsExportRequest();
        var format = request.Format?.Trim().ToLowerInvariant();

        if (format is "pdf" or "json")
        {
            return Result.Failure<AnalyticsExportResponse>(AnalyticsErrors.ExportFormatUnsupported);
        }

        if (format != AnalyticsExports.Csv)
        {
            return Result.Failure<AnalyticsExportResponse>(
                AnalyticsErrors.ExportInvalid("An export needs a format: csv."));
        }

        var (query, problem) = AnalyticsQueryPayloads.Normalise(request.Query);

        if (query is null)
        {
            return Result.Failure<AnalyticsExportResponse>(AnalyticsErrors.ExportInvalid(problem!));
        }

        var definition = AnalyticsQueryPayloads.Serialise(query);

        if (!AnalyticsQueryPayloads.Fits(definition))
        {
            return Result.Failure<AnalyticsExportResponse>(
                AnalyticsErrors.ExportInvalid("The query is too large to export."));
        }

        // The dataset the caller could open on screen, under the dashboard's own checks. A refusal
        // there is the refusal here, with the same code, and no job is recorded for it.
        var dataset = await _datasets.ReadAsync(query, caller.Value, cancellationToken).ConfigureAwait(false);

        if (dataset.IsFailure)
        {
            return Result.Failure<AnalyticsExportResponse>(dataset.Error);
        }

        var now = _context.Calendar.UtcNow;
        var file = AnalyticsCsvWriter.Write(dataset.Value, query.Metrics);

        var export = AnalyticsExport.Request(
            AnalyticsExportId.New(), caller.Value, AnalyticsExports.Csv, definition, now);

        export.Complete(
            file,
            Encoding.UTF8.GetByteCount(file),
            AnalyticsExports.NewToken(),
            now,
            AnalyticsExports.Lifetime);

        _exports.Add(export);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(AnalyticsExports.ToResponse(export, now));
    }
}

/// <summary>One of the caller's exports, and its link while the link is live.</summary>
[Authorize(Permission = AnalyticsPermissions.CreatorRead)]
public sealed record GetAnalyticsExportQuery : IQuery<AnalyticsExportResponse>
{
    public required string ExportId { get; init; }
}

public sealed class GetAnalyticsExportQueryHandler
    : IQueryHandler<GetAnalyticsExportQuery, AnalyticsExportResponse>
{
    private readonly AnalyticsQueryContext _context;
    private readonly IAnalyticsExportRepository _exports;

    public GetAnalyticsExportQueryHandler(AnalyticsQueryContext context, IAnalyticsExportRepository exports)
    {
        _context = context;
        _exports = exports;
    }

    public async Task<Result<AnalyticsExportResponse>> Handle(
        GetAnalyticsExportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<AnalyticsExportResponse>(caller.Error);
        }

        if (AnalyticsExports.ParseId(query.ExportId) is not { } id)
        {
            return Result.Failure<AnalyticsExportResponse>(AnalyticsErrors.ExportNotFound);
        }

        var export = await _exports.FindOwnedAsync(id, caller.Value, cancellationToken).ConfigureAwait(false);

        return export is null
            ? Result.Failure<AnalyticsExportResponse>(AnalyticsErrors.ExportNotFound)
            : Result.Success(AnalyticsExports.ToResponse(export, _context.Calendar.UtcNow));
    }
}

/// <summary>
/// The file itself.
/// <para>
/// <b>Two credentials, either sufficient.</b> The link's token, so the file opens in a browser or a
/// share sheet that holds no session; or the owner's own session, for a client that downloads with its
/// bearer token. No permission attribute, because the token-bearing request has no caller — the
/// handler is the check, and it never answers without one of the two.
/// </para>
/// </summary>
[AllowAnonymous(
    "The token in the link is the credential. A browser or share sheet following it holds no " +
    "session, and the handler answers only to that token or to the owner's own session, never to " +
    "neither.")]
public sealed record GetAnalyticsExportFileQuery : IQuery<AnalyticsExportFile>
{
    public required string ExportId { get; init; }

    public string? Token { get; init; }
}

public sealed class GetAnalyticsExportFileQueryHandler
    : IQueryHandler<GetAnalyticsExportFileQuery, AnalyticsExportFile>
{
    private readonly AnalyticsQueryContext _context;
    private readonly IAnalyticsExportRepository _exports;

    public GetAnalyticsExportFileQueryHandler(AnalyticsQueryContext context, IAnalyticsExportRepository exports)
    {
        _context = context;
        _exports = exports;
    }

    public async Task<Result<AnalyticsExportFile>> Handle(
        GetAnalyticsExportFileQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (AnalyticsExports.ParseId(query.ExportId) is not { } id)
        {
            return Result.Failure<AnalyticsExportFile>(AnalyticsErrors.ExportNotFound);
        }

        AnalyticsExport? export;

        if (!string.IsNullOrWhiteSpace(query.Token))
        {
            if (query.Token.Length > AnalyticsExport.DownloadTokenLength)
            {
                return Result.Failure<AnalyticsExportFile>(AnalyticsErrors.ExportNotFound);
            }

            export = await _exports.FindByTokenAsync(id, query.Token, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var caller = _context.RequireUser();

            if (caller.IsFailure)
            {
                return Result.Failure<AnalyticsExportFile>(caller.Error);
            }

            export = await _exports.FindOwnedAsync(id, caller.Value, cancellationToken).ConfigureAwait(false);
        }

        if (export is null || export.Status != AnalyticsExportStatus.Ready || export.FileText is null)
        {
            return Result.Failure<AnalyticsExportFile>(AnalyticsErrors.ExportNotFound);
        }

        if (export.HasExpired(_context.Calendar.UtcNow))
        {
            return Result.Failure<AnalyticsExportFile>(AnalyticsErrors.ExportExpired);
        }

        var definition = AnalyticsQueryPayloads.Deserialise(export.DefinitionJson);
        var name = string.Create(
            CultureInfo.InvariantCulture,
            $"diaspora-analytics-{Slug(definition.Subject?.Type)}-{Slug(definition.Range?.From)}-{Slug(definition.Range?.To)}.csv");

        // A byte-order mark, so a spreadsheet opened on Windows reads the file as UTF-8 rather than
        // mangling every non-ASCII name in it.
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(export.FileText)).ToArray();

        return Result.Success(new AnalyticsExportFile(name, "text/csv; charset=utf-8", bytes));
    }

    private static string Slug(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "all"
            : new string([.. value.Where(character => char.IsAsciiLetterOrDigit(character) || character == '-')]);
}
