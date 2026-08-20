namespace Diaspora.Modules.Location.Contracts.V1.Requests;

/// <summary>
/// Declares that the member lived, or lives, in a country.
/// <para>
/// A claim. Submitting evidence for it is a Trust operation against
/// <c>POST /api/v1/trust/verifications/country</c>, and this endpoint neither requires nor
/// triggers one — a member may state their history without being asked to prove it, and the
/// two flows are deliberately independent.
/// </para>
/// </summary>
public sealed record DeclareCountryExperienceRequest
{
    public required string CountryCode { get; init; }

    public required DateOnly ArrivedOn { get; init; }

    /// <summary>Omit while the member still lives there.</summary>
    public DateOnly? DepartedOn { get; init; }

    /// <summary>"Diaspora Member", "Student", "Worker". Free text.</summary>
    public string? Role { get; init; }

    /// <summary><c>None</c>, <c>Basic</c>, <c>Conversational</c>, <c>Fluent</c>, <c>Native</c>.</summary>
    public string? LanguageFamiliarity { get; init; }

    public string? CulturalFamiliarity { get; init; }

    /// <summary>
    /// Replaces the whole set, which is what the wizard's save actually means — it posts the
    /// full array every time.
    /// </summary>
    public IReadOnlyList<string>? ExpertiseAreas { get; init; }
}
