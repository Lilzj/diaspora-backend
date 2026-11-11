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
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record GetOrganizationQuery : IQuery<OrganizationResponse>
{
    public Guid? OrganizationId { get; init; }

    public string? Slug { get; init; }
}
