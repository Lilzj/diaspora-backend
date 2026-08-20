namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// One country's emergency information.
/// <para>
/// <b>The response a client should be able to render offline, and shaped so it can.</b> Everything
/// somebody needs while frightened is here in one document — the numbers, what they are called
/// locally, whether they answer at night — with no identifier to resolve against another module and
/// no second call to make. A client that caches one response per country is doing the right thing,
/// and this shape is what makes that possible.
/// </para>
/// </summary>
public sealed record EmergencyContactsResponse
{
    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }

    /// <summary>
    /// A line rendered above the numbers — "dial 999 from a landline, 112 from a mobile". Verbatim.
    /// </summary>
    public string? Guidance { get; init; }

    public IReadOnlyList<EmergencyContactResponse> Contacts { get; init; } = [];

    /// <summary>When somebody last checked the whole page against an official source.</summary>
    public DateTimeOffset? LastReviewedAt { get; init; }

    /// <summary>The locale actually served, after the fallback.</summary>
    public required string ResolvedLocale { get; init; }

    /// <summary>The concurrency token to send back on an edit. Editors only.</summary>
    public int? Revision { get; init; }
}

/// <summary>
/// One number somebody in trouble should call.
/// <para>
/// The label is what this country calls the service, translated where a translation exists — never
/// the platform's English name for the kind, because the body a Francophone country calls for police
/// is the gendarmerie.
/// </para>
/// </summary>
public sealed record EmergencyContactResponse
{
    /// <summary>The stable kind key — <c>police</c>. What a client switches on for an icon.</summary>
    public required string Kind { get; init; }

    /// <summary>What this country calls it, localized.</summary>
    public required string Label { get; init; }

    public required string Number { get; init; }

    public string? AlternateNumber { get; init; }

    /// <summary>Hours, languages, coverage. Localized.</summary>
    public string? Note { get; init; }

    /// <summary>Rendered as a badge, because it is what somebody checks at two in the morning.</summary>
    public required bool IsAvailableAllHours { get; init; }

    public required int Position { get; init; }
}
