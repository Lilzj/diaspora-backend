namespace Diaspora.Api.Platform;

/// <summary>
/// What <c>GET /</c> tells a caller it knows nothing about: which platform this is, and where to ask
/// whether it is up.
/// <para>
/// <b>Deliberately no more (BSEC-009).</b> The root used to list every composed module with a version
/// string, which told an unauthenticated caller which capability areas this deployment runs:
/// reconnaissance for free. That list is behind the operations permission now, at
/// <c>GET /api/v1/platform/operations/modules</c>.
/// </para>
/// </summary>
/// <param name="Platform">The platform's name.</param>
/// <param name="Health">The path of the liveness probe.</param>
public sealed record PlatformInfoResponse(string Platform, string Health);
