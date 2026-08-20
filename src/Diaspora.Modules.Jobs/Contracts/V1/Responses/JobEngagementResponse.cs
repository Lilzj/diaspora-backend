namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// The counters a posting carries.
/// <para>
/// <see cref="ApplicationCount"/> is shown to candidates as well as employers, and that is a
/// deliberate choice in the candidate's favour: knowing a role has four hundred applicants is
/// exactly what tells someone whether an afternoon spent applying is well spent.
/// </para>
/// </summary>
public sealed record JobEngagementResponse
{
    public required long ViewCount { get; init; }

    public required int ApplicationCount { get; init; }

    public required int SaveCount { get; init; }
}
