using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Intelligence.Directory;

// ── Embassies ───────────────────────────────────────────────────────────────────────────────

/// <summary>Adds a mission to the embassy directory.</summary>
[RequirePermission(IntelligencePermissions.ManageDirectory)]
[Auditable]
public sealed record CreateEmbassyCommand : ICommand<EmbassyResponse>
{
    public required CreateEmbassyRequest Request { get; init; }
}

/// <summary>
/// Rewrites a mission's entry.
/// <para>
/// <c>[Transactional]</c> because the profile, the contact details, the four capability flags, the
/// media reference, the authority link and three collections are separate aggregate operations
/// applied from one editing screen. A partial apply is how a mission ends up with new opening hours
/// and last year's emergency number.
/// </para>
/// </summary>
[RequirePermission(IntelligencePermissions.ManageDirectory)]
[Auditable]
[Transactional(Reason =
    "Profile, contact, capabilities, media, authority link and three collections are separate " +
    "aggregate operations applied from one editing screen.")]
public sealed record UpdateEmbassyCommand : ICommand<EmbassyResponse>
{
    public required Guid EmbassyId { get; init; }

    public required UpdateEmbassyRequest Request { get; init; }
}

/// <summary>Raises a claim that a mission is what it says it is.</summary>
[RequirePermission(IntelligencePermissions.ManageAuthorities)]
[Auditable]
[Transactional(Reason =
    "The submission opens a Trust case and the case identifier is then stored on the mission. " +
    "Losing the second write means every decision Trust returns has nowhere to land.")]
public sealed record SubmitEmbassyVerificationCommand : ICommand<EmbassyResponse>
{
    public required Guid EmbassyId { get; init; }

    public required SubmitAuthorityVerificationRequest Request { get; init; }
}

/// <summary>Removes a mission from the directory. The soft delete; restorable.</summary>
[RequirePermission(IntelligencePermissions.ManageDirectory)]
[Auditable]
public sealed record ArchiveEmbassyCommand : ICommand
{
    public required Guid EmbassyId { get; init; }
}

/// <summary>Returns an archived mission to the directory, verification intact.</summary>
[RequirePermission(IntelligencePermissions.ManageDirectory)]
[Auditable]
public sealed record RestoreEmbassyCommand : ICommand<EmbassyResponse>
{
    public required Guid EmbassyId { get; init; }
}

// ── Emergency information ───────────────────────────────────────────────────────────────────

/// <summary>
/// Saves one country's emergency information.
/// <para>
/// <b>Create and update are one command, because emergency information is edited as a page.</b>
/// Somebody checks the whole thing against an official source and saves it; there is no workflow in
/// which they add one number in isolation. A partial-update API would let a country end up with a
/// police number from one source and an ambulance number from another, checked six months apart, with
/// nothing recording which.
/// </para>
/// <para>
/// <c>[Transactional]</c> because the guidance line, the contact set, the translations and the review
/// stamp are four aggregate operations — and a country left holding new contacts with translations
/// that name the old ones would render half its page in the wrong language.
/// </para>
/// </summary>
[RequirePermission(IntelligencePermissions.ManageDirectory)]
[Auditable]
[Transactional(Reason =
    "Guidance, contacts, translations and the review stamp are four aggregate operations, and a " +
    "partial apply leaves translations pointing at contacts that no longer exist.")]
public sealed record SaveEmergencyContactsCommand : ICommand<EmergencyContactsResponse>
{
    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }

    public required SaveEmergencyContactsRequest Request { get; init; }
}

// ── Visa advisories ─────────────────────────────────────────────────────────────────────────

/// <summary>Opens a draft visa advisory.</summary>
[RequirePermission(IntelligencePermissions.ManageVisas)]
[Auditable]
public sealed record CreateVisaAdvisoryCommand : ICommand<VisaAdvisoryResponse>
{
    public required CreateVisaAdvisoryRequest Request { get; init; }
}

/// <summary>
/// Rewrites a visa advisory.
/// <para>
/// Editable while published, unlike a notice: an advisory is a standing description of what is true
/// now, and freezing it would make it wrong the day a fee changed. What preserves the history is that
/// a rule change publishes a notice, and notices are permanent.
/// </para>
/// </summary>
[RequirePermission(IntelligencePermissions.ManageVisas)]
[Auditable]
[Transactional(Reason =
    "Content, sourcing, dates, the authority link and three collections are separate aggregate " +
    "operations applied from one editing screen.")]
public sealed record UpdateVisaAdvisoryCommand : ICommand<VisaAdvisoryResponse>
{
    public required Guid VisaAdvisoryId { get; init; }

    public required UpdateVisaAdvisoryRequest Request { get; init; }
}

/// <summary>
/// Makes a visa advisory visible.
/// <para>
/// The authority must be verified, exactly as it must be for a notice. An advisory carries the same
/// implicit claim — that this is what the rules are — and a member turned away at a border is not
/// helped by the distinction between a notice and an advisory.
/// </para>
/// </summary>
[RequirePermission(IntelligencePermissions.ManageVisas)]
[Auditable]
public sealed record PublishVisaAdvisoryCommand : ICommand<VisaAdvisoryResponse>
{
    public required Guid VisaAdvisoryId { get; init; }

    public int? ExpectedRevision { get; init; }
}

/// <summary>Withdraws a visa advisory. The soft delete; restorable.</summary>
[RequirePermission(IntelligencePermissions.ManageVisas)]
[Auditable]
public sealed record ArchiveVisaAdvisoryCommand : ICommand
{
    public required Guid VisaAdvisoryId { get; init; }
}

/// <summary>Returns an archived advisory to the desk as a draft.</summary>
[RequirePermission(IntelligencePermissions.ManageVisas)]
[Auditable]
public sealed record RestoreVisaAdvisoryCommand : ICommand<VisaAdvisoryResponse>
{
    public required Guid VisaAdvisoryId { get; init; }
}

// ── Public holidays ─────────────────────────────────────────────────────────────────────────

/// <summary>Adds a day to the public holiday calendar.</summary>
[RequirePermission(IntelligencePermissions.ManageDirectory)]
[Auditable]
[Transactional(Reason =
    "The entry and its translations are two aggregate operations, and a holiday saved without its " +
    "translations renders under whichever community's spelling the editor happened to type.")]
public sealed record CreatePublicHolidayCommand : ICommand<PublicHolidayResponse>
{
    public required CreatePublicHolidayRequest Request { get; init; }
}

/// <summary>Rewrites a calendar entry. The country cannot change.</summary>
[RequirePermission(IntelligencePermissions.ManageDirectory)]
[Auditable]
[Transactional(Reason = "The entry and its translations are two aggregate operations.")]
public sealed record UpdatePublicHolidayCommand : ICommand<PublicHolidayResponse>
{
    public required Guid PublicHolidayId { get; init; }

    public required UpdatePublicHolidayRequest Request { get; init; }
}

/// <summary>Removes a calendar entry. The soft delete; restorable.</summary>
[RequirePermission(IntelligencePermissions.ManageDirectory)]
[Auditable]
public sealed record ArchivePublicHolidayCommand : ICommand
{
    public required Guid PublicHolidayId { get; init; }
}

/// <summary>Returns an archived calendar entry, exactly as it was.</summary>
[RequirePermission(IntelligencePermissions.ManageDirectory)]
[Auditable]
public sealed record RestorePublicHolidayCommand : ICommand<PublicHolidayResponse>
{
    public required Guid PublicHolidayId { get; init; }
}
