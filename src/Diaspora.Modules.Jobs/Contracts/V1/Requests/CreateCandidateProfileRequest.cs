namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>
/// Creates a candidate profile.
/// <para>
/// Only a headline is required. A profile wizard that demanded a full work history before it
/// saved anything is one nobody finishes, and an abandoned profile helps no one.
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
    /// <summary>The one line beside a name. "Registered nurse, 8 years, ICU."</summary>
    public required string Headline { get; init; }

    public string? Summary { get; init; }

    /// <summary>Where the candidate is now. Optional — someone mid-move may have no answer.</summary>
    public string? CountryCode { get; init; }

    public string? CityName { get; init; }
}
