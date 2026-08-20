using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;

namespace Diaspora.Modules.Jobs.Domain.Candidates;

/// <summary>
/// What an employer may search candidates on, and on whose authority.
/// <para>
/// <b>The bottom three fields are not optional context — they are the access control.</b> A
/// candidate's chosen visibility is meaningless unless the query enforces it, and enforcing it
/// means the repository must know which employer is asking and whether that employer is
/// actually verified. Passing a bare "is verified" boolean from a caller would make the whole
/// setting advisory; passing the employer means the query checks the trust mark itself.
/// </para>
/// <para>
/// <b>There is no salary-expectation filter, deliberately.</b> A candidate's expected salary is
/// never disclosed to employers and never searchable — letting a recruiter filter on it would
/// hand one side of a negotiation the other side's floor.
/// </para>
/// </summary>
public sealed record CandidateSearchFilter
{
    /// <summary>Free text, matched against the headline and summary.</summary>
    public string? Text { get; init; }

    /// <summary>Normalised skill keys. A candidate matching any of them qualifies.</summary>
    public IReadOnlyList<string>? SkillKeys { get; init; }

    /// <summary>Where the candidate is now.</summary>
    public string? CountryCode { get; init; }

    /// <summary>A country the candidate says they would work in.</summary>
    public string? PreferredCountryCode { get; init; }

    /// <summary>Only candidates who can work in this country without sponsorship.</summary>
    public string? AuthorizedToWorkIn { get; init; }

    public ExperienceLevel? MinimumExperience { get; init; }

    public EducationLevel? MinimumEducation { get; init; }

    /// <summary>Languages the candidate must speak. ISO 639-1.</summary>
    public IReadOnlyList<string>? LanguageCodes { get; init; }

    public RemotePreference? RemotePreference { get; init; }

    /// <summary>Only candidates who say they are looking right now.</summary>
    public bool OpenToWorkOnly { get; init; } = true;

    // ── Authority ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The employer running the search. Null means no employer context, which can only see
    /// profiles their owners made public.
    /// </summary>
    public EmployerProfileId? SearchingEmployerId { get; init; }

    /// <summary>Whether the caller holds a platform staff role.</summary>
    public bool IsStaff { get; init; }
}
