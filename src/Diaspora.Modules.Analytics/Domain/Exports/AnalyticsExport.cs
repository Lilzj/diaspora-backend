using Diaspora.Modules.Analytics.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Analytics.Domain.Exports;

/// <summary>
/// One export a member asked for, and the file it produced.
/// <para>
/// FRONTEND-PHASE15-GAP-026. <b>The file is the dataset the member could already see, as rows.</b>
/// It is produced by the same dataset read the dashboard makes, under the same access checks, so an
/// export can never carry a figure its owner could not have opened on screen. A dataset is at most a
/// few hundred rows, which is why it is produced while the request waits and kept on the row rather
/// than written to object storage.
/// </para>
/// <para>
/// <b>Short-lived.</b> The download stops answering at <see cref="ExpiresAt"/>; an exported file
/// leaves every protection the app provides, and a link that works for ever is a leak on a timer.
/// </para>
/// </summary>
public sealed class AnalyticsExport : AggregateRoot<AnalyticsExportId>
{
    public const int DownloadTokenLength = 64;

    private AnalyticsExport(
        AnalyticsExportId id,
        Guid ownerUserId,
        string format,
        string definitionJson,
        DateTimeOffset requestedAt) : base(id)
    {
        OwnerUserId = ownerUserId;
        Format = format;
        DefinitionJson = definitionJson;
        RequestedAt = requestedAt;
        Status = AnalyticsExportStatus.Queued;
    }

    private AnalyticsExport() { }

    public Guid OwnerUserId { get; private set; }

    /// <summary><c>csv</c>. The only format this deployment produces.</summary>
    public string Format { get; private set; } = "csv";

    /// <summary>The client's <c>AnalyticsQuery</c> the export was produced from, as JSON.</summary>
    public string DefinitionJson { get; private set; } = "{}";

    public AnalyticsExportStatus Status { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>The produced file. Null until ready.</summary>
    public string? FileText { get; private set; }

    public long? SizeBytes { get; private set; }

    /// <summary>
    /// The capability the download link carries, so the file opens in a browser that holds no
    /// session. Random, per export, and useless once <see cref="ExpiresAt"/> passes.
    /// </summary>
    public string? DownloadToken { get; private set; }

    public string? FailureReason { get; private set; }

    public int Revision { get; private set; }

    public static AnalyticsExport Request(
        AnalyticsExportId id,
        Guid ownerUserId,
        string format,
        string definitionJson,
        DateTimeOffset requestedAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionJson);

        return new AnalyticsExport(id, ownerUserId, format, definitionJson, requestedAt);
    }

    public void Complete(
        string fileText, long sizeBytes, string downloadToken, DateTimeOffset at, TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(fileText);
        ArgumentException.ThrowIfNullOrWhiteSpace(downloadToken);

        Status = AnalyticsExportStatus.Ready;
        FileText = fileText;
        SizeBytes = Math.Max(0, sizeBytes);
        DownloadToken = downloadToken;
        CompletedAt = at;
        ExpiresAt = at.Add(lifetime);
        Revision++;
    }

    public void Fail(string reason, DateTimeOffset at)
    {
        Status = AnalyticsExportStatus.Failed;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "The export could not be produced." : reason.Trim();
        CompletedAt = at;
        Revision++;
    }

    /// <summary>Whether the file has passed its lifetime. Read, never stored: the clock decides.</summary>
    public bool HasExpired(DateTimeOffset now) =>
        Status == AnalyticsExportStatus.Ready && ExpiresAt is { } expiresAt && now >= expiresAt;
}

/// <summary>Where an export is. <c>expired</c> is derived from the clock, so it is not a stored state.</summary>
public enum AnalyticsExportStatus
{
    Queued = 0,
    Ready = 1,
    Failed = 2
}
