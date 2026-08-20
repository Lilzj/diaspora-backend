namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// What this deployment's Country Intelligence Platform can actually do.
/// <para>
/// <b>A client asking rather than assuming.</b> Which notice categories exist, which emergency kinds
/// have icons, whether search is answering and whether authority verification is available are all
/// deployment facts — and a client that hard-codes them ships a picker that offers a category this
/// deployment removed and omits the one it added.
/// </para>
/// <para>
/// It is also the operational answer to "why did search behave differently in production": the engine
/// name is on here, and a support conversation that starts with it is a much shorter one.
/// </para>
/// </summary>
public sealed record IntelligenceCapabilitiesResponse
{
    /// <summary>The notice category catalogue, ordered. What a picker offers.</summary>
    public IReadOnlyList<NoticeCategoryResponse> Categories { get; init; } = [];

    /// <summary>The emergency contact vocabulary. What a client finds an icon for.</summary>
    public IReadOnlyList<EmergencyContactKindResponse> EmergencyKinds { get; init; } = [];

    /// <summary>The authority types an editor may choose from. PascalCase.</summary>
    public IReadOnlyList<string> AuthorityTypes { get; init; } = [];

    /// <summary>The severities, in ascending order of urgency. PascalCase.</summary>
    public IReadOnlyList<string> Severities { get; init; } = [];

    /// <summary>The sort keys a listing accepts. Kebab, because they travel in a query string.</summary>
    public IReadOnlyList<string> SortOptions { get; init; } = [];

    /// <summary>The search engine answering here. Diagnostic.</summary>
    public required string SearchEngine { get; init; }

    /// <summary>Whether it can answer right now.</summary>
    public required bool SearchAvailable { get; init; }

    /// <summary>Whether anything on this deployment can verify an authority.</summary>
    public required bool AuthorityVerificationAvailable { get; init; }

    /// <summary>The source validators in force, by module. Diagnostic.</summary>
    public IReadOnlyList<string> SourceValidators { get; init; } = [];

    /// <summary>The analytics sinks registered, by name. Diagnostic.</summary>
    public IReadOnlyList<string> AnalyticsSinks { get; init; } = [];

    /// <summary>How far ahead a country dashboard looks for holidays, in days.</summary>
    public required int HolidayHorizonDays { get; init; }
}

/// <summary>One kind of emergency contact this deployment recognises.</summary>
public sealed record EmergencyContactKindResponse
{
    public required string Key { get; init; }

    /// <summary>The platform's name for it. Editorial screens only — readers see the country's.</summary>
    public required string Name { get; init; }

    public string? Icon { get; init; }

    public required int Order { get; init; }

    /// <summary>Whether a country directory missing this one is worth flagging to an editor.</summary>
    public required bool IsEssential { get; init; }
}
