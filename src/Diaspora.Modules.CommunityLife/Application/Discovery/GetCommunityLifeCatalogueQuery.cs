using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Discovery;

/// <summary>
/// Everything a client needs to render the pickers and the filter bar, in one call.
/// <para>
/// Anonymous, and deliberately one endpoint rather than three: a form that had to make three round
/// trips before it could show a dropdown would be three chances to render half a form.
/// </para>
/// </summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record GetCommunityLifeCatalogueQuery : IQuery<CommunityLifeCatalogueResponse>
{
    public bool IncludeRetired { get; init; }
}
