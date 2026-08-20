using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Jobs.Domain.Employers;

/// <summary>
/// Everything the employer directory can narrow on.
/// <para>
/// <see cref="HiringIntoCountry"/> is the one that matters most to this platform's members and
/// the one a naive implementation gets wrong: it matches the employer's declared hiring regions
/// rather than where it is based, because a Berlin company that hires across the EU is exactly
/// what a candidate in Lisbon is looking for.
/// </para>
/// </summary>
public sealed record EmployerQueryFilter
{
    /// <summary>Free text, matched against the display and legal names.</summary>
    public string? Text { get; init; }

    /// <summary>Where the organisation is based.</summary>
    public string? CountryCode { get; init; }

    /// <summary>A country the employer says it hires into. Not where it is based.</summary>
    public string? HiringIntoCountry { get; init; }

    public IReadOnlyList<EmployerOrganizationType>? OrganizationTypes { get; init; }

    public IReadOnlyList<CompanySize>? Sizes { get; init; }

    /// <summary>Only employers carrying a Trust mark.</summary>
    public bool VerifiedOnly { get; init; }

    /// <summary>Only employers that say they sponsor visas somewhere.</summary>
    public bool SponsorsVisasOnly { get; init; }

    /// <summary>Only employers with at least one posting open.</summary>
    public bool HiringNowOnly { get; init; }

    /// <summary>Employers this member belongs to. The dashboard's switcher.</summary>
    public UserId? MemberId { get; init; }

    /// <summary>
    /// Whether suspended and closed employers are in scope. Staff only — a candidate must not
    /// be shown an employer the platform has barred from recruiting.
    /// </summary>
    public bool IncludeInactive { get; init; }
}
