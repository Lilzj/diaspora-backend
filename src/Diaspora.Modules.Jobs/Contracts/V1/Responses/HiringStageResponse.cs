namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// One column on a hiring board.
/// <para>
/// <see cref="Name"/> is the employer's word and <see cref="Kind"/> is the platform's. Both,
/// because the board has to read the way the employer thinks while every cross-employer figure
/// has to be computable without parsing anybody's stage names.
/// </para>
/// </summary>
public sealed record HiringStageResponse
{
    /// <summary>The stable handle an application stores. Unchanged by a rename.</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    /// <summary><c>Applied</c>, <c>Screening</c>, <c>Interview</c>, <c>Offer</c>, <c>Hired</c>, …</summary>
    public required string Kind { get; init; }

    public required int Order { get; init; }

    /// <summary>False for the applied and hired stages, which no board may lose.</summary>
    public required bool IsRemovable { get; init; }

    public required int ApplicationCount { get; init; }
}
