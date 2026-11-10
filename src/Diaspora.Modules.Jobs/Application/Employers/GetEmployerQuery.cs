using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;

namespace Diaspora.Modules.Jobs.Application.Employers;

/// <summary>
/// One employer, by id or slug.
/// <para>
/// **Gaps G-1 and G-2, closed.** Every job posting names an employer, and there was no route on
/// which to open one: `GET /employers/{id}` answered **404** and `GET /jobs/employers/{id}`
/// answered **405** — the path existed for `POST` and `PUT` only. `src/app/employer/[id].tsx` in
/// the client is written against exactly this, and `src/lib/jobs-hiring-api.ts` records the same
/// conclusion reached from the backend source ("NG-04, confirmed").
/// </para>
/// <para>
/// `EmployerMapper.ToResponse` already draws the outsider's view — it takes a nullable
/// <c>viewerRole</c> and hides the registry details and the member list when there is none — so
/// what was missing was genuinely only the query and the route, not a projection.
/// </para>
/// </summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record GetEmployerQuery : IQuery<EmployerResponse>
{
    /// <summary>A GUID or a slug. A public profile is linkable, so both must resolve.</summary>
    public required string IdOrSlug { get; init; }
}
