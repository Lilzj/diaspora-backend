using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.Users;

namespace Diaspora.Application.Identity.Abstractions;

/// <summary>
/// The single authority on where an authenticated member should be sent and what they
/// may do once they get there.
/// <para>
/// Every flow that produces tokens routes through this. Controllers never branch on
/// access state themselves — that logic exists once, here, so adding a state cannot
/// leave a stale <c>if</c> behind in some endpoint.
/// </para>
/// </summary>
public interface IAccessGateway
{
    AccessDecision Resolve(User user, DateTimeOffset now);
}
