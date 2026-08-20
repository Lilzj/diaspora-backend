using System.ComponentModel.DataAnnotations;

namespace Diaspora.Api.Middleware;

/// <summary>
/// How an incoming request path is normalised onto the platform's canonical route space.
/// <para>
/// <b>This section exists because of one long-standing mismatch, not because route shapes are
/// negotiable.</b> Every module on this platform mints its prefix as <c>/api/v1/{module}</c> and an
/// architecture test asserts each one. The React Native client resolves its base URL to
/// <c>https://api.diaspora.app/v1</c> and calls <c>/conversations</c>, <c>/notifications</c> and the
/// rest — so every request it makes arrives one segment short. That gap was first recorded in B13's
/// alignment report, restated by B13.2 as V-1, and it has never had an owner: the deployment has no
/// reverse proxy in this repository, and no module may move to a different prefix on its own without
/// becoming the only module on one.
/// </para>
/// <para>
/// <b>The resolution is a rewrite, not a second set of routes.</b> A request whose path begins with
/// a declared version segment and does not already begin with <see cref="RootPrefix"/> is rewritten
/// once, before routing, so the endpoint that answers it is the same endpoint, with the same
/// authorisation metadata, the same rate-limit policy and the same telemetry. Nothing is mapped
/// twice, no route constant changes, and the canonical path stays the only one in the OpenAPI
/// document — which is what stops the alias becoming a second public API surface with its own
/// lifetime.
/// </para>
/// <para>
/// <b>Switchable, because a deployment that puts a real gateway in front should turn it off.</b>
/// When an ingress rewrites <c>/v1</c> to <c>/api/v1</c> the platform never sees an aliased path and
/// this costs one string comparison per request; when the ingress is removed or misconfigured, the
/// alias is what keeps the client working. Setting <see cref="EnablePrefixCompatibility"/> to
/// <c>false</c> makes the canonical prefix the only one served.
/// </para>
/// </summary>
public sealed class ApiRoutingOptions
{
    public const string SectionName = "Api:Routing";

    /// <summary>
    /// The segment every served API route begins with. Not configurable in practice — it is
    /// asserted per module by an architecture test — but named here so the rewrite reads as a
    /// statement about the platform rather than a hard-coded string in a middleware.
    /// </summary>
    [Required]
    public string RootPrefix { get; set; } = "/api";

    /// <summary>
    /// Whether a path that begins with a declared version segment is rewritten under
    /// <see cref="RootPrefix"/>. Default <c>true</c>: the client in this repository needs it, and a
    /// deployment whose gateway already does the rewrite never reaches the branch.
    /// </summary>
    public bool EnablePrefixCompatibility { get; set; } = true;

    /// <summary>
    /// The version segments a client may address directly. One today, and the list is what makes
    /// <c>/v2</c> a configuration change rather than a second middleware.
    /// </summary>
    public string[] VersionSegments { get; set; } = ["/v1"];
}
