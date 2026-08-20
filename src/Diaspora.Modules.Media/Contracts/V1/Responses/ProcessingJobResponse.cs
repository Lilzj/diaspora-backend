namespace Diaspora.Modules.Media.Contracts.V1.Responses;

/// <summary>
/// How the work over one asset went.
/// <para>
/// Exposed so "why does my photograph have no thumbnail" has an answer that does not require
/// reading a log. <see cref="Status"/> distinguishes <c>skipped</c> — no processor in this
/// deployment could do any of it — from <c>failed</c>, which is the difference between a
/// missing capability and a broken one.
/// </para>
/// </summary>
public sealed record ProcessingJobResponse
{
    public required Guid JobId { get; init; }

    public required Guid MediaId { get; init; }

    /// <summary><c>pending</c>, <c>running</c>, <c>succeeded</c>, <c>partiallySucceeded</c>, <c>failed</c>, <c>skipped</c>, <c>cancelled</c>.</summary>
    public required string Status { get; init; }

    public required int Attempts { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public string? FailureReason { get; init; }

    public required IReadOnlyList<ProcessingTaskResponse> Tasks { get; init; }
}
