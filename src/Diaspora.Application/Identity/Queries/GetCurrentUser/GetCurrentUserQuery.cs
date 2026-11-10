using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Queries.GetCurrentUser;

/// <summary>Returns the signed-in member's own profile. Backs <c>GET /auth/me</c>.</summary>
[RequirePermission("identity:profile:read")]
public sealed record GetCurrentUserQuery : IQuery<UserProfileResponse>;
