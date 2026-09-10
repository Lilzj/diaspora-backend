namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// The employer, as a job detail renders it.
/// <para>
/// <see cref="AverageResponseHours"/> and <see cref="GhostingRate"/> are here deliberately, and
/// they are the fields most job boards would never publish. Applying into silence is the defining
/// experience of job hunting and it is worst for people applying across a border who cannot
/// follow up in person. Both are null until the employer has a large enough sample — a rate
/// computed from one event is noise presented as a fact about a named organisation.
/// </para>
/// </summary>
public sealed record JobEmployerSummaryResponse
{
    public required Guid EmployerId { get; init; }
    /// <summary>
    /// The member who owns this employer, and therefore who a message about it reaches.
    /// <para>
    /// **An employer is not a user.** Its identifier is a profile's, and the client was sending that
    /// as a conversation's recipient — so the thread was addressed to something that is not a
    /// person. Nothing could deliver to it, the other side could never open it, and it rendered
    /// with no name on it.
    /// </para>
    /// </summary>
    public Guid? OwnerUserId { get; init; }

    public required string Slug { get; init; }

    public required string DisplayName { get; init; }

    public string? Tagline { get; init; }

    /// <summary><c>Company</c>, <c>RecruitmentAgency</c>, …</summary>
    public required string OrganizationType { get; init; }

    /// <summary>Whether candidates are dealing with an intermediary rather than the hirer.</summary>
    public required bool IsIntermediary { get; init; }

    public required string CompanySize { get; init; }

    public required string CountryCode { get; init; }

    public required bool IsVerified { get; init; }

    /// <summary>The badge label Trust granted. Rendered, never interpreted.</summary>
    public string? VerificationBadge { get; init; }

    public Guid? LogoMediaId { get; init; }

    public Guid? CoverMediaId { get; init; }

    public string? WebsiteUrl { get; init; }

    public string? CareersUrl { get; init; }

    /// <summary>Mean hours to a first response. Null until there is anything to say.</summary>
    public double? AverageResponseHours { get; init; }

    /// <summary>Share of applications never answered. Null below the sample threshold.</summary>
    public double? GhostingRate { get; init; }

    /// <summary>Share of offers candidates accepted. Null below the sample threshold.</summary>
    public double? OfferAcceptanceRate { get; init; }
}
