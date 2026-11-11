using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Intelligence.Catalogue;

/// <summary>
/// Adds a kind of official notice.
/// <para>
/// Administrators only. The category catalogue decides how urgent a notice is by default and which
/// published contract its publication produces — it is the shape of the whole product rather than a
/// per-country editorial choice.
/// </para>
/// </summary>
[RequirePermission(IntelligencePermissions.ManageCatalogue)]
[Auditable]
public sealed record CreateNoticeCategoryCommand : ICommand<NoticeCategoryResponse>
{
    public required CreateNoticeCategoryRequest Request { get; init; }
}

/// <summary>
/// Rewrites a notice category. Everything except the key, which is the contract.
/// <para>
/// <c>[Transactional]</c> because the label and the translation set are separate aggregate
/// operations, and a rename that landed without its translations would show a client the new English
/// name beside the old French one.
/// </para>
/// </summary>
[RequirePermission(IntelligencePermissions.ManageCatalogue)]
[Auditable]
[Transactional(Reason =
    "The label and the localization set are separate aggregate operations applied from one screen.")]
public sealed record UpdateNoticeCategoryCommand : ICommand<NoticeCategoryResponse>
{
    public required string Key { get; init; }

    public required UpdateNoticeCategoryRequest Request { get; init; }
}

/// <summary>
/// Retires a category.
/// <para>
/// Refused while any notice is still filed under it, including archived ones — restoring a notice
/// into a category that is gone would strand it somewhere no listing shows.
/// </para>
/// </summary>
[RequirePermission(IntelligencePermissions.ManageCatalogue)]
[Auditable]
public sealed record ArchiveNoticeCategoryCommand : ICommand
{
    public required string Key { get; init; }
}

/// <summary>Returns an archived category to the catalogue.</summary>
[RequirePermission(IntelligencePermissions.ManageCatalogue)]
[Auditable]
public sealed record RestoreNoticeCategoryCommand : ICommand<NoticeCategoryResponse>
{
    public required string Key { get; init; }
}

/// <summary>
/// Returns the platform's authority for a country, creating and confirming it the first time.
/// Notices, visa advisories and embassy details written by platform admins name it as their
/// issuer, and readers are shown "Posted by Diaspora platform admin". Idempotent.
/// </summary>
[RequirePermission(IntelligencePermissions.ManageNotices)]
[Auditable]
public sealed record EnsurePlatformAuthorityCommand : ICommand<IssuingAuthorityResponse>
{
    public required string CountryCode { get; init; }
}

/// <summary>Records a body whose statements this platform will treat as official.</summary>
[RequirePermission(IntelligencePermissions.ManageAuthorities)]
[Auditable]
public sealed record CreateIssuingAuthorityCommand : ICommand<IssuingAuthorityResponse>
{
    public required CreateIssuingAuthorityRequest Request { get; init; }
}

/// <summary>Rewrites an authority record. The country cannot change.</summary>
[RequirePermission(IntelligencePermissions.ManageAuthorities)]
[Auditable]
public sealed record UpdateIssuingAuthorityCommand : ICommand<IssuingAuthorityResponse>
{
    public required Guid IssuingAuthorityId { get; init; }

    public required UpdateIssuingAuthorityRequest Request { get; init; }
}

/// <summary>
/// Raises a claim that a body is what it says it is.
/// <para>
/// <b><c>[Transactional]</c>, and this is the case that most needs it.</b> The handler submits to a
/// verification provider and then stores the case identifier the inbound projection correlates on. A
/// failure between the two leaves a case open with a reviewer that this module can never match a
/// decision to — which is exactly the defect B8 shipped, and it is silent: the submission succeeds,
/// the reviewer approves, and the badge never appears.
/// </para>
/// </summary>
[RequirePermission(IntelligencePermissions.ManageAuthorities)]
[Auditable]
[Transactional(Reason =
    "The submission opens a Trust case and the case identifier is then stored on the subject. " +
    "Losing the second write means every decision Trust returns has nowhere to land.")]
public sealed record SubmitAuthorityVerificationCommand : ICommand<IssuingAuthorityResponse>
{
    public required Guid IssuingAuthorityId { get; init; }

    public required SubmitAuthorityVerificationRequest Request { get; init; }
}

/// <summary>
/// Retires an authority.
/// <para>
/// Refused while any published or superseded notice still names it: those notices render its name,
/// and archiving it would leave live official information attributed to a body the register hides.
/// </para>
/// </summary>
[RequirePermission(IntelligencePermissions.ManageAuthorities)]
[Auditable]
public sealed record ArchiveIssuingAuthorityCommand : ICommand
{
    public required Guid IssuingAuthorityId { get; init; }
}

/// <summary>Returns an archived authority to the register, verification intact.</summary>
[RequirePermission(IntelligencePermissions.ManageAuthorities)]
[Auditable]
public sealed record RestoreIssuingAuthorityCommand : ICommand<IssuingAuthorityResponse>
{
    public required Guid IssuingAuthorityId { get; init; }
}
