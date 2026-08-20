namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>
/// Registers a résumé version against an already-uploaded document.
/// <para>
/// A Media reference, never a file. B9 never sees a stream — the bytes, the virus scan, the
/// access policy and the signed URL an employer downloads through are all B5's.
/// </para>
/// </summary>
public sealed record AddResumeRequest
{
    /// <summary>B5's reference to the uploaded document.</summary>
    public required Guid MediaId { get; init; }

    /// <summary>What the candidate calls this version. "Nursing — UK", "General".</summary>
    public required string Label { get; init; }

    /// <summary>Make this the version attached when no other is chosen.</summary>
    public bool IsDefault { get; init; }
}
