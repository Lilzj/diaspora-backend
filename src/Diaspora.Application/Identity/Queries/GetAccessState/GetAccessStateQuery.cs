using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Queries.GetAccessState;

/// <summary>
/// Returns the member's position on the progressive access ladder along with the Access
/// Gateway's routing decision. Backs <c>GET /access/state</c>.
/// </summary>
[RequirePermission("identity:profile:read")]
public sealed record GetAccessStateQuery : IQuery<AccessStateResponse>;
