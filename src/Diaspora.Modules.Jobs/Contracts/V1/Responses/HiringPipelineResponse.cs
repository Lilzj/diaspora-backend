namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// A posting's hiring board, with the count in each column.
/// <para>
/// The counts come from the application table rather than from a stored figure on the stage: a
/// denormalised count would be wrong every time somebody moved a candidate and right only after
/// a sweep, and this is a screen an employer stares at while making decisions.
/// </para>
/// </summary>
public sealed record HiringPipelineResponse
{
    public required Guid PipelineId { get; init; }

    public required Guid JobId { get; init; }

    public required string JobTitle { get; init; }

    public required IReadOnlyList<HiringStageResponse> Stages { get; init; }

    /// <summary>The whole funnel by platform status, beside the employer's own columns.</summary>
    public required IReadOnlyDictionary<string, int> StatusCounts { get; init; }

    public required int TotalApplications { get; init; }

    public required int Revision { get; init; }
}
