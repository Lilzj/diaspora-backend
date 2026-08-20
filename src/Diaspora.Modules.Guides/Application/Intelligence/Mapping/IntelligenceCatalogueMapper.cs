using Diaspora.Modules.Guides.Application.Intelligence.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Intelligence;

namespace Diaspora.Modules.Guides.Application.Intelligence.Mapping;

/// <summary>
/// Turns notice categories and issuing authorities into what a client renders.
/// <para>
/// Static, like every mapper in this module. See <see cref="OfficialNoticeMapper"/> for why.
/// </para>
/// </summary>
public static class IntelligenceCatalogueMapper
{
    /// <summary>A category as a reader sees it: localized, without the editorial fields.</summary>
    public static NoticeCategoryResponse ToResponse(NoticeCategory category, string? locale)
    {
        ArgumentNullException.ThrowIfNull(category);

        var (name, description) = category.Localized(locale);

        return new NoticeCategoryResponse
        {
            NoticeCategoryId = category.Id.Value,
            Key = category.Key,
            Name = name,
            Description = description,
            Icon = category.Icon,
            Order = category.Order,
            DefaultSeverity = IntelligenceWireNames.ToWire(category.DefaultSeverity),
            AlertFamily = IntelligenceWireNames.ToWire(category.AlertFamily),
            IsActive = category.IsActive
        };
    }

    /// <summary>
    /// A category as its editor sees it: the default label, the revision, and which locales exist.
    /// <para>
    /// The <em>default</em> name rather than a localized one, deliberately. An editor renaming a
    /// category needs to see the string they are about to overwrite, and showing them a translation
    /// of it would mean their edit silently lands somewhere they were not looking.
    /// </para>
    /// </summary>
    public static NoticeCategoryResponse ToEditorResponse(NoticeCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);

        return new NoticeCategoryResponse
        {
            NoticeCategoryId = category.Id.Value,
            Key = category.Key,
            Name = category.Name,
            Description = category.Description,
            Icon = category.Icon,
            Order = category.Order,
            DefaultSeverity = IntelligenceWireNames.ToWire(category.DefaultSeverity),
            AlertFamily = IntelligenceWireNames.ToWire(category.AlertFamily),
            IsActive = category.IsActive,
            Revision = category.Revision,
            AvailableLocales =
                [.. category.Localizations.Select(localization => localization.Locale).Order()]
        };
    }

    public static IssuingAuthorityResponse ToResponse(
        IssuingAuthority authority, bool forEditor = false)
    {
        ArgumentNullException.ThrowIfNull(authority);

        return new IssuingAuthorityResponse
        {
            IssuingAuthorityId = authority.Id.Value,
            CountryCode = authority.CountryCode,
            Name = authority.Name,
            ShortName = authority.ShortName,
            AuthorityType = IntelligenceWireNames.ToWire(authority.AuthorityType),
            Description = authority.Description,
            Website = authority.Website,
            ContactEmail = authority.ContactEmail,
            ContactPhone = authority.ContactPhone,
            IsVerified = authority.IsVerified,
            VerifiedAt = authority.VerifiedAt,
            IsActive = authority.IsActive,
            Revision = forEditor ? authority.Revision : null
        };
    }
}
