using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Jobs.V1.Events;

/// <summary>
/// An employer profile was created.
/// <para>
/// Mostly of interest to moderation and to onboarding: a new employer is exactly the moment to
/// prompt for verification, and exactly the moment a reviewer might want to look. It is not a
/// discovery signal — an employer with no postings is nothing to show a candidate.
/// </para>
/// </summary>
public sealed record EmployerRegisteredIntegrationEvent : IntegrationEvent
{
    public required Guid EmployerId { get; init; }

    /// <summary>The member who registered it and holds the first owner membership.</summary>
    public required Guid OwnerUserId { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>The deep-link handle.</summary>
    public required string Slug { get; init; }

    /// <summary>
    /// <c>Company</c>, <c>RecruitmentAgency</c>, <c>NonProfit</c> and so on. Worth having on the
    /// contract because an intermediary registering is a different moderation prospect from an
    /// employer hiring for itself.
    /// </summary>
    public required string OrganizationType { get; init; }

    /// <summary>ISO 3166-1 alpha-2, upper case.</summary>
    public required string CountryCode { get; init; }

    public required DateTimeOffset RegisteredAt { get; init; }
}
