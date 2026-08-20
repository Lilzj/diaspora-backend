using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Guides.Application.Discovery;

/// <summary>
/// The discovery query. The client's Guides tab, and every filter on it.
/// <para>
/// Anonymous callers are served: a member looking for how to register a residence permit should
/// find the answer before being asked to make an account, and a knowledge base that demands one
/// first has no way of persuading anybody to.
/// </para>
/// <para>
/// The free-text term goes through <c>IGuideSearchEngine</c> rather than around it, so a
/// deployment that installs a real engine changes the quality of this screen and nothing else.
/// </para>
/// </summary>
public sealed record SearchGuidesQuery : IQuery<CursorPagedResult<GuideCardResponse>>
{
    public required SearchGuidesRequest Request { get; init; }
}
