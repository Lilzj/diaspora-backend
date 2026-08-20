namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// A reader viewed the guide, and optionally how thoroughly.
/// <para>
/// Uniqueness is claimed by the client rather than fingerprinted by the server — honest
/// analytics at the cost the platform is willing to pay. Nothing here is a source of truth for
/// anything except measurement.
/// </para>
/// </summary>
public sealed record RecordGuideViewRequest
{
    /// <summary>The client's "first view by this reader" claim.</summary>
    public bool? IsUniqueView { get; init; }

    /// <summary>Whether the reader reached the end.</summary>
    public bool? Completed { get; init; }

    /// <summary>Seconds spent reading, when the client measured them.</summary>
    public long? ReadSeconds { get; init; }
}
