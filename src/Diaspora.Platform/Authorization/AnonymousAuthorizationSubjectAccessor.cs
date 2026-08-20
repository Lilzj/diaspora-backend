using Diaspora.Contracts.Authorization;

namespace Diaspora.Platform.Authorization;

/// <summary>
/// The kernel's default subject accessor: everyone is anonymous.
/// <para>
/// Replaced by the Identity module. Left in place, it denies everything — which is the
/// correct failure mode for an authorisation component and the opposite of the B1
/// placeholder it succeeds.
/// </para>
/// </summary>
public sealed class AnonymousAuthorizationSubjectAccessor : IAuthorizationSubjectAccessor
{
    public AuthorizationSubject Current => AuthorizationSubject.Anonymous;
}
