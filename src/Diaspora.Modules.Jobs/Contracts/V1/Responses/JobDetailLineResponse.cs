namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>One bulleted line on a posting.</summary>
public sealed record JobDetailLineResponse
{
    public required Guid LineId { get; init; }

    public required string Text { get; init; }

    /// <summary>
    /// On a requirement, whether it is a hard bar. Rendering the distinction is what stops a
    /// qualified candidate self-rejecting over a preference.
    /// </summary>
    public required bool IsEssential { get; init; }
}
