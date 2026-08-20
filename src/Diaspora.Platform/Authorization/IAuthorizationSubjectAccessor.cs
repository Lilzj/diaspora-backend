using Diaspora.Contracts.Authorization;

namespace Diaspora.Platform.Authorization;

/// <summary>
/// Supplies the <see cref="AuthorizationSubject"/> for the caller of the current request.
/// <para>
/// The seam between "who is calling" (Identity's business) and "what may they do" (the
/// kernel's). The kernel's authorisation handlers depend on this and nothing else, so a
/// module can gate an endpoint on a tier without referencing an access state, a user
/// aggregate, or a claim name.
/// </para>
/// <para>
/// The kernel ships an implementation that reports every caller as
/// <see cref="AuthorizationSubject.Anonymous"/>. The Identity module replaces it. That is
/// deliberately the safe default: a host with no identity module grants nothing, rather
/// than granting everything the way B1's placeholder permission service did.
/// </para>
/// </summary>
public interface IAuthorizationSubjectAccessor
{
    AuthorizationSubject Current { get; }
}
