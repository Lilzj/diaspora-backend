namespace Diaspora.Modules.Media.Contracts.V1.Responses;

/// <summary>
/// One file a scanner refused, as a staff queue needs to see it.
/// <para>
/// <b>Deliberately not <see cref="MediaAssetResponse"/>.</b> That record carries a URL, and a
/// quarantined file has nothing servable behind one — handing a moderator a link that is
/// guaranteed to fail is worse than not offering it. What this carries instead is what somebody
/// deciding whether to release it needs: what the scanner said, who uploaded it, and what the
/// file was attached to.
/// </para>
/// </summary>
public sealed record QuarantinedMediaResponse
{
    public required Guid MediaId { get; init; }

    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    public required string Kind { get; init; }

    public required long SizeBytes { get; init; }

    public required Guid UploadedBy { get; init; }

    /// <summary>The scanner's verdict — <c>Infected</c>, and rarely anything else here.</summary>
    public required string ScanStatus { get; init; }

    /// <summary>Which scanner said so. A second opinion starts by knowing whose the first was.</summary>
    public string? Scanner { get; init; }

    /// <summary>What it says it found. The single most useful field on this record.</summary>
    public string? ThreatName { get; init; }

    public DateTimeOffset? ScannedAt { get; init; }

    /// <summary>What the file was attached to, so a moderator can see what breaks either way.</summary>
    public required string OwnerType { get; init; }

    public Guid? OwnerId { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
