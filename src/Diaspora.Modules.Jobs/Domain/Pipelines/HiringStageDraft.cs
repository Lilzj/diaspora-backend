using Diaspora.Modules.Jobs.Domain.Enums;

namespace Diaspora.Modules.Jobs.Domain.Pipelines;

/// <summary>
/// One stage as the board editor sends it.
/// <para>
/// <see cref="Key"/> is supplied for a stage that already exists and omitted for a new one.
/// Preserving it is what keeps every application in that column pointing at the right stage
/// when the employer renames or reorders the board.
/// </para>
/// </summary>
public sealed record HiringStageDraft
{
    /// <summary>Omitted for a new stage; supplied to keep an existing one's identity.</summary>
    public string? Key { get; init; }

    public required string? Name { get; init; }

    public required HiringStageKind Kind { get; init; }
}
