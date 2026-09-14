namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>
/// Creates a candidate profile.
/// <para>
/// Nothing is required. Applying for a job creates an empty profile on its own, so this route is
/// for a member who wants to fill one in before they apply — and a form that demanded a headline
/// before it saved anything would put back the step that change removed.
/// </para>
/// <para>
/// <b>The profile is created private.</b> Visibility is deliberately not on this request:
/// changing who can see you is its own operation, because it is the setting a candidate changes
/// urgently and on its own, and burying it in a create form would make "hide me" a risky thing to
/// do.
/// </para>
/// </summary>
public sealed record CreateCandidateProfileRequest
{
    /// <summary>The one line beside a name. "Registered nurse, 8 years, ICU." Optional.</summary>
    public string? Headline { get; init; }

    public string? Summary { get; init; }

    /// <summary>Where the candidate is now. Optional — someone mid-move may have no answer.</summary>
    public string? CountryCode { get; init; }

    public string? CityName { get; init; }
}
