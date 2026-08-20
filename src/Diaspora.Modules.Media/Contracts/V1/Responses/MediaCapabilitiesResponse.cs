namespace Diaspora.Modules.Media.Contracts.V1.Responses;

/// <summary>
/// What this deployment can actually do with media.
/// <para>
/// <b>The endpoint that lets a client stop guessing.</b> Whether videos can be transcoded,
/// whether uploads are scanned, how large a file may be and how large a chunk should be are
/// all deployment facts. A client that hardcodes them is a client that breaks when a
/// deployment changes, and a client that assumes them is a client that lets a member upload
/// 400 MB before finding out.
/// </para>
/// <para>
/// It is also the honest answer to "does this platform scan uploads?".
/// <see cref="MalwareScanningEnabled"/> is false when no scanner is registered, and no amount
/// of reading the code from the outside would tell you that.
/// </para>
/// </summary>
public sealed record MediaCapabilitiesResponse
{
    /// <summary>Largest upload accepted, keyed by media kind.</summary>
    public required IReadOnlyDictionary<string, long> MaxUploadBytes { get; init; }

    /// <summary>Above this, a chunked session is required.</summary>
    public required long DirectUploadCeilingBytes { get; init; }

    public required long ChunkSizeBytes { get; init; }

    public required int SessionLifetimeHours { get; init; }

    /// <summary>How many uploads one member may have open at once.</summary>
    public required int MaxConcurrentUploads { get; init; }

    /// <summary>Content types accepted, keyed by media kind.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> AcceptedContentTypes { get; init; }

    /// <summary>Variant names that can actually be produced here. Empty when no codec is registered.</summary>
    public required IReadOnlyList<string> AvailableVariants { get; init; }

    /// <summary>Processing operations something can perform, keyed by media kind.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> SupportedOperations { get; init; }

    /// <summary>False when this deployment has no malware scanner registered.</summary>
    public required bool MalwareScanningEnabled { get; init; }

    /// <summary>Whether uploads may be paused and resumed. True on every deployment.</summary>
    public required bool ResumableUploads { get; init; }

    /// <summary>How long a signed link lives, in seconds.</summary>
    public required int SignedUrlLifetimeSeconds { get; init; }
}
