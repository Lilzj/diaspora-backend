namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// Adds a kind of official notice.
/// <para>
/// The key is the contract and can never be changed: it is what a seed declares, what a URL carries,
/// what every notice is filed under and what a member's notification preference stores. Renaming is
/// free; rekeying is not offered.
/// </para>
/// </summary>
public sealed record CreateNoticeCategoryRequest
{
    /// <summary>Lower-case kebab.</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? Icon { get; init; }

    public int Order { get; init; }

    /// <summary>PascalCase. What a notice here is worth before an editor decides otherwise.</summary>
    public string? DefaultSeverity { get; init; }

    /// <summary>
    /// PascalCase — <c>None</c>, <c>Emergency</c>, <c>GovernmentAnnouncement</c>. Which extra
    /// published contract this category's notices produce.
    /// </summary>
    public string? AlertFamily { get; init; }

    public IReadOnlyList<CategoryLocalizationInput> Localizations { get; init; } = [];
}

/// <summary>Rewrites a notice category. Everything except the key.</summary>
public sealed record UpdateNoticeCategoryRequest
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? Icon { get; init; }

    public int Order { get; init; }

    public string? DefaultSeverity { get; init; }

    public string? AlertFamily { get; init; }

    /// <summary>Supplied means replace; absent means leave.</summary>
    public IReadOnlyList<CategoryLocalizationInput>? Localizations { get; init; }

    public int? ExpectedRevision { get; init; }
}

/// <summary>One translated label.</summary>
public sealed record CategoryLocalizationInput
{
    public required string Locale { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }
}

/// <summary>Records a body whose statements this platform will treat as official.</summary>
public sealed record CreateIssuingAuthorityRequest
{
    /// <summary>Upper-case ISO 3166-1 alpha-2. Cannot be changed afterwards.</summary>
    public required string CountryCode { get; init; }

    public required string Name { get; init; }

    /// <summary>PascalCase — <c>ImmigrationDepartment</c>, <c>Embassy</c>, <c>Other</c>.</summary>
    public required string AuthorityType { get; init; }

    public string? ShortName { get; init; }

    public string? Description { get; init; }

    public string? Website { get; init; }

    public string? ContactEmail { get; init; }

    public string? ContactPhone { get; init; }
}

/// <summary>Rewrites an authority record. The country cannot change.</summary>
public sealed record UpdateIssuingAuthorityRequest
{
    public required string Name { get; init; }

    public required string AuthorityType { get; init; }

    public string? ShortName { get; init; }

    public string? Description { get; init; }

    public string? Website { get; init; }

    public string? ContactEmail { get; init; }

    public string? ContactPhone { get; init; }

    public int? ExpectedRevision { get; init; }
}

/// <summary>
/// Raises a claim that a body is what it says it is.
/// <para>
/// <b>Evidence is optional here and required by Trust's handler.</b> That asymmetry is deliberate: a
/// deployment with a contributed provider — a foreign ministry's accredited-missions list — may need
/// no documents at all, and the request cannot know which provider will answer. A submission with no
/// evidence that reaches the Trust provider is refused by Trust, with Trust's own message.
/// </para>
/// </summary>
public sealed record SubmitAuthorityVerificationRequest
{
    public IReadOnlyList<AuthorityEvidenceInput> Evidence { get; init; } = [];

    /// <summary>Anything a reviewer should know that no field holds.</summary>
    public string? Notes { get; init; }
}

/// <summary>One piece of proof. A media reference, never a URL.</summary>
public sealed record AuthorityEvidenceInput
{
    public required Guid MediaId { get; init; }

    /// <summary>A Trust document-type name. Absent stamps this module's default.</summary>
    public string? DocumentType { get; init; }
}
