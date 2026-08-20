using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Organizations;

/// <summary>
/// One organization, by identifier or by slug.
/// <para>
/// Anonymous. A body's page is how somebody who has just arrived in a city finds a place of worship
/// or a support group, and requiring an account first would put the platform behind the need.
/// </para>
/// </summary>
public sealed record GetOrganizationQuery : IQuery<OrganizationResponse>
{
    public Guid? OrganizationId { get; init; }

    public string? Slug { get; init; }
}
