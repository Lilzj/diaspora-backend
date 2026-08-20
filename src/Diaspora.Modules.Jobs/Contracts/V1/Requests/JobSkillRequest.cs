namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>
/// One skill as the editor sends it.
/// <para>
/// A name, not a key. The module normalises it through the same function the candidate side
/// uses, so "React.js" and "reactjs" land on one key and can find each other. A skill the
/// catalogue has not seen is accepted rather than refused — the catalogue is an aid, not a gate
/// on hiring.
/// </para>
/// </summary>
public sealed record JobSkillRequest
{
    public required string Name { get; init; }

    /// <summary><c>Beginner</c> … <c>Expert</c>. Omitted means unspecified.</summary>
    public string? MinimumProficiency { get; init; }

    public bool IsEssential { get; init; } = true;
}
