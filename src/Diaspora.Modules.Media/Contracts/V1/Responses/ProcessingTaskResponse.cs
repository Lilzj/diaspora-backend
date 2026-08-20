namespace Diaspora.Modules.Media.Contracts.V1.Responses;

/// <summary>One step of a processing job, and how it went.</summary>
public sealed record ProcessingTaskResponse
{
    /// <summary><c>extractMetadata</c>, <c>scanForMalware</c>, <c>generateThumbnails</c>, and so on.</summary>
    public required string Operation { get; init; }

    /// <summary>
    /// <c>pending</c>, <c>running</c>, <c>succeeded</c>, <c>failed</c>, <c>unsupported</c>,
    /// <c>notApplicable</c>.
    /// <para>
    /// <c>unsupported</c> is a deployment fact rather than an error: nothing registered claims
    /// the operation.
    /// </para>
    /// </summary>
    public required string Status { get; init; }

    /// <summary>What happened, in words. The processor's name, or the reason it declined.</summary>
    public string? Detail { get; init; }

    public double? DurationMilliseconds { get; init; }
}
