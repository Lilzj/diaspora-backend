namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// One country's emergency information, in full.
/// <para>
/// <b>Create-and-update are one request, and one endpoint, because emergency information is edited
/// as a page.</b> Somebody checks the whole thing against an official source and saves it; there is
/// no workflow in which they add one number in isolation. A partial-update API would let a country
/// end up with a police number from one source and an ambulance number from another, checked six
/// months apart, with nothing recording which.
/// </para>
/// </summary>
public sealed record SaveEmergencyContactsRequest
{
    /// <summary>A line rendered above the numbers. Verbatim.</summary>
    public string? Guidance { get; init; }

    /// <summary>
    /// The whole set. A repeated kind is refused rather than collapsed: two police numbers is a
    /// contradiction an editor has to resolve, not one the platform should resolve by list order.
    /// </summary>
    public IReadOnlyList<EmergencyContactInput> Contacts { get; init; } = [];

    /// <summary>
    /// Translations of the labels and notes. Never of the numbers — a translation must not be able
    /// to change which number a frightened person dials.
    /// </summary>
    public IReadOnlyList<EmergencyLocalizationInput> Localizations { get; init; } = [];

    /// <summary>Whether this save also counts as a review of the whole page.</summary>
    public bool MarkReviewed { get; init; }

    public int? ExpectedRevision { get; init; }
}

/// <summary>One number somebody in trouble should call.</summary>
public sealed record EmergencyContactInput
{
    /// <summary>Lower-case kebab — <c>police</c>, <c>mental-health</c>. Open-ended by design.</summary>
    public required string Kind { get; init; }

    /// <summary>What this country calls the service. Written, not derived from the kind.</summary>
    public required string Label { get; init; }

    /// <summary>Kept as written. Frequently short and not internationally dialable.</summary>
    public required string Number { get; init; }

    public string? AlternateNumber { get; init; }

    /// <summary>Hours, languages, coverage.</summary>
    public string? Note { get; init; }

    public bool IsAvailableAllHours { get; init; }
}

/// <summary>One emergency contact's label and note in one locale.</summary>
public sealed record EmergencyLocalizationInput
{
    public required string Locale { get; init; }

    /// <summary>Must name a kind the country lists.</summary>
    public required string Kind { get; init; }

    public required string Label { get; init; }

    public string? Note { get; init; }
}

/// <summary>Opens a draft visa advisory.</summary>
public sealed record CreateVisaAdvisoryRequest
{
    /// <summary>Upper-case ISO 3166-1 alpha-2 — the destination.</summary>
    public required string CountryCode { get; init; }

    /// <summary>The nationality the rules apply to, or absent for everybody.</summary>
    public string? AppliesToCountryCode { get; init; }

    public required Guid AuthorityId { get; init; }

    public required string Title { get; init; }

    public required string Summary { get; init; }
}

/// <summary>
/// Everything about a visa advisory an editor may rewrite.
/// <para>
/// Editable while published, unlike a notice, and the asymmetry is the point: an advisory is a
/// standing description of what is true <em>now</em>, and freezing it would make it wrong the day a
/// fee changed. What preserves the history is that a rule change publishes a notice, and notices are
/// permanent.
/// </para>
/// </summary>
public sealed record UpdateVisaAdvisoryRequest
{
    public required string Title { get; init; }

    public required string Summary { get; init; }

    public string? Content { get; init; }

    /// <summary>The visa class — <c>Student</c>. Free text; every country names its own.</summary>
    public string? VisaCategory { get; init; }

    /// <summary>As the authority states it — "10 to 15 working days".</summary>
    public string? ProcessingTime { get; init; }

    public Guid? AuthorityId { get; init; }

    public string? OfficialReference { get; init; }

    public string? SourceUrl { get; init; }

    /// <summary>Where somebody applies. This platform does not.</summary>
    public string? ApplicationUrl { get; init; }

    public DateTimeOffset? EffectiveFrom { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    /// <inheritdoc cref="UpdateOfficialNoticeRequest.SetDates" />
    public bool SetDates { get; init; }

    /// <summary>Conditions to satisfy. Supplied means replace; absent means leave.</summary>
    public IReadOnlyList<string>? Requirements { get; init; }

    /// <summary>Papers to bring. Supplied means replace; absent means leave.</summary>
    public IReadOnlyList<string>? Documents { get; init; }

    /// <inheritdoc cref="Requirements" />
    public IReadOnlyList<Guid>? RelatedGuideIds { get; init; }

    /// <inheritdoc cref="Requirements" />
    public IReadOnlyList<NoticeLocalizationInput>? Localizations { get; init; }

    public int? ExpectedRevision { get; init; }
}

/// <summary>Adds a day to the public holiday calendar.</summary>
public sealed record CreatePublicHolidayRequest
{
    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }

    public required string Name { get; init; }

    /// <summary>PascalCase — <c>National</c>, <c>Religious</c>, <c>EmbassyClosure</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>The day it falls on. Truncated to whole days in UTC.</summary>
    public required DateTimeOffset ObservedOn { get; init; }

    /// <summary>The last day of a multi-day observance. Absent means a single day.</summary>
    public DateTimeOffset? ObservedUntil { get; init; }

    public string? Description { get; init; }

    /// <summary>The area it applies to, or absent for nationwide.</summary>
    public string? Region { get; init; }

    /// <summary>The fact that changes somebody's week.</summary>
    public bool ClosesGovernmentOffices { get; init; }

    /// <summary>Frequently a different set of days from the government's.</summary>
    public bool ClosesEmbassies { get; init; }

    public IReadOnlyList<CategoryLocalizationInput> Localizations { get; init; } = [];
}

/// <summary>Rewrites a calendar entry. The country cannot change.</summary>
public sealed record UpdatePublicHolidayRequest
{
    public required string Name { get; init; }

    public required string Kind { get; init; }

    public required DateTimeOffset ObservedOn { get; init; }

    public DateTimeOffset? ObservedUntil { get; init; }

    public string? Description { get; init; }

    public string? Region { get; init; }

    public bool ClosesGovernmentOffices { get; init; }

    public bool ClosesEmbassies { get; init; }

    /// <summary>Supplied means replace; absent means leave.</summary>
    public IReadOnlyList<CategoryLocalizationInput>? Localizations { get; init; }

    public int? ExpectedRevision { get; init; }
}
