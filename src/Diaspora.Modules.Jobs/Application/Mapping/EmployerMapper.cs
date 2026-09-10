using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Jobs.Application.Mapping;

/// <summary>
/// Turns an employer into the shapes the wire carries.
/// <para>
/// <b>Two fields are audience-gated and the gating is here rather than in a client.</b> The legal
/// name and registration number, and the member list, are sent only to the employer's own team
/// and to staff. Neither is secret exactly, but a candidate browsing employers has no business
/// with a company's registration number or a list of who works in its HR department, and putting
/// the decision in the mapper means no route can forget it.
/// </para>
/// </summary>
public static class EmployerMapper
{
    /// <summary>The full profile. <paramref name="viewerRole"/> null means an outside viewer.</summary>
    public static EmployerResponse ToResponse(
        EmployerProfile employer,
        int openJobCount,
        EmployerMemberRole? viewerRole,
        bool isStaff = false)
    {
        ArgumentNullException.ThrowIfNull(employer);

        // The team sees the registry details and the member list; outsiders do not.
        var isInsider = isStaff || viewerRole is not null;

        return new EmployerResponse
        {
            EmployerId = employer.Id.Value,
            Slug = employer.Slug.Value,
            DisplayName = employer.Identity.DisplayName,
            LegalName = isInsider ? employer.Identity.LegalName : null,
            RegistrationNumber = isInsider ? employer.Identity.RegistrationNumber : null,
            OrganizationType = JobWireNames.ToWire(employer.Identity.OrganizationType),
            IsIntermediary = employer.IsIntermediary,
            CompanySize = JobWireNames.ToWire(employer.Identity.Size),
            FoundedYear = employer.Identity.FoundedYear,
            CountryCode = employer.PrimaryCountryCode,
            Description = employer.Description,
            Tagline = employer.Branding.Tagline,
            LogoMediaId = employer.Branding.LogoMediaId,
            CoverMediaId = employer.Branding.CoverMediaId,
            ContactEmail = employer.Contact.Email,
            ContactPhone = employer.Contact.Phone,
            WebsiteUrl = employer.Contact.WebsiteUrl,
            CareersUrl = employer.Contact.CareersUrl,
            Status = JobWireNames.ToWire(employer.Status),
            IsVerified = employer.IsVerified,
            VerificationState = JobWireNames.ToWire(employer.TrustMark.State),
            VerificationBadge = employer.TrustMark.Badge,

            HiringRegions = [.. employer.HiringRegions.Select(region =>
                new HiringRegionResponse
                {
                    CountryCode = region.CountryCode,
                    SponsorsVisas = region.SponsorsVisas
                })],

            Members = isInsider
                ? [.. employer.Members.Select(member => new EmployerMemberResponse
                {
                    UserId = member.UserId.Value,
                    Role = JobWireNames.ToWire(member.Role),
                    JoinedAt = member.JoinedAt
                })]
                : null,

            Responsiveness = ToResponsiveness(employer),
            OpenJobCount = openJobCount,
            RegisteredAt = employer.CreatedAt,
            Revision = employer.Revision,
            ViewerRole = viewerRole is { } role ? JobWireNames.ToWire(role) : null
        };
    }

    /// <summary>The employer chip a job detail renders.</summary>
    public static JobEmployerSummaryResponse ToSummary(EmployerProfile employer)
    {
        ArgumentNullException.ThrowIfNull(employer);

        var responsiveness = employer.Responsiveness;

        return new JobEmployerSummaryResponse
        {
            EmployerId = employer.Id.Value,
            // Who a message about this reaches. See the field's own summary.
            OwnerUserId = employer.OwnerUserId.Value,
            Slug = employer.Slug.Value,
            DisplayName = employer.Identity.DisplayName,
            Tagline = employer.Branding.Tagline,
            OrganizationType = JobWireNames.ToWire(employer.Identity.OrganizationType),
            IsIntermediary = employer.IsIntermediary,
            CompanySize = JobWireNames.ToWire(employer.Identity.Size),
            CountryCode = employer.PrimaryCountryCode,
            IsVerified = employer.IsVerified,
            VerificationBadge = employer.TrustMark.Badge,
            LogoMediaId = employer.Branding.LogoMediaId,
            CoverMediaId = employer.Branding.CoverMediaId,
            WebsiteUrl = employer.Contact.WebsiteUrl,
            CareersUrl = employer.Contact.CareersUrl,

            // Null below the sample threshold. A rate from one event is noise presented as a
            // fact about a named organisation.
            AverageResponseHours = responsiveness.AverageResponseHours,
            GhostingRate = responsiveness.GhostingRate,
            OfferAcceptanceRate = responsiveness.OfferAcceptanceRate
        };
    }

    public static EmployerResponsivenessResponse ToResponsiveness(EmployerProfile employer)
    {
        ArgumentNullException.ThrowIfNull(employer);

        return new EmployerResponsivenessResponse
        {
            AverageResponseHours = employer.Responsiveness.AverageResponseHours,
            GhostingRate = employer.Responsiveness.GhostingRate,
            OfferAcceptanceRate = employer.Responsiveness.OfferAcceptanceRate,
            DecidedCount = employer.Responsiveness.DecidedCount
        };
    }

    /// <summary>The caller's role at this employer, or null when they are not a member.</summary>
    public static EmployerMemberRole? ViewerRole(EmployerProfile employer, UserId? userId) =>
        userId is null ? null : employer.RoleOf(userId);
}
