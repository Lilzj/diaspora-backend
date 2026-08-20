namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>
/// One bulleted line as the editor sends it.
/// <para>
/// <see cref="LineId"/> is omitted for a new line and echoed back for an existing one, which is
/// what lets a reorder update rows rather than delete and recreate them.
/// </para>
/// </summary>
public sealed record JobDetailLineRequest
{
    public Guid LineId { get; init; }

    public required string Text { get; init; }

    /// <summary>
    /// On a requirement, whether it is a hard bar or a nice-to-have. Worth stating: "desirable"
    /// requirements presented as mandatory are the biggest single reason qualified people do not
    /// apply.
    /// </summary>
    public bool IsEssential { get; init; } = true;
}
