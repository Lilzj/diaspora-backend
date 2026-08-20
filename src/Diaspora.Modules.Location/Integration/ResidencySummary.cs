using Diaspora.Modules.Location.Domain.Enums;

namespace Diaspora.Modules.Location.Integration;

/// <summary>
/// One country a member has lived in, with the verification state Trust last published.
/// <para>
/// A flat record of primitives on purpose. It crosses a module boundary, and a consumer that
/// had to reference Location's value objects to read a country code would be coupled to this
/// module's internals rather than to its contract.
/// </para>
/// </summary>
public sealed record ResidencySummary
{
    public required string CountryCode { get; init; }

    public required string CountryName { get; init; }

    public required string Flag { get; init; }

    public required DateOnly ArrivedOn { get; init; }

    /// <summary>Null while the member still lives there.</summary>
    public DateOnly? DepartedOn { get; init; }

    public required bool IsCurrentResidence { get; init; }

    public required int Years { get; init; }

    public required ExperienceLevel ExperienceLevel { get; init; }

    /// <summary>
    /// Trust's decision, as last heard. <see cref="ResidencyVerificationState.Verified"/> here
    /// already accounts for expiry.
    /// </summary>
    public required ResidencyVerificationState VerificationState { get; init; }

    /// <summary>The shorthand nearly every consumer actually wants.</summary>
    public bool IsVerified => VerificationState == ResidencyVerificationState.Verified;

    public required IReadOnlyList<string> ExpertiseAreas { get; init; }
}
