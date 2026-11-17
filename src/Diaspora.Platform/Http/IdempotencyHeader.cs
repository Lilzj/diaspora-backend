using Microsoft.AspNetCore.Http;

namespace Diaspora.Platform.Http;

/// <summary>
/// Reads the <c>Idempotency-Key</c> header.
/// <para>
/// <b>One owner for a wire contract.</b> BSOLID-006 found three copies of this class, one per
/// module, with three different file hashes; there were four — the fourth is declared at the foot
/// of <c>IntegrationsEndpointContributor.cs</c> rather than in a file of its own, so a scan keyed
/// on the filename missed it. All four spelled the header the same way, which is luck rather than
/// design: the client sends <c>Idempotency-Key</c> uniformly, the published contract documents it,
/// and four owners are four chances to drift from both.
/// </para>
/// <para>
/// <b>Reading the header is all this does.</b> The three remaining questions — whether a body
/// field may stand in for it, how a key is scoped, and what a duplicate means — are answered
/// elsewhere on purpose. Creator and CommunityProgramme merge the body field inside
/// <c>Normalise</c>, where the key is also scoped by operation and by caller, a rule
/// <c>IdempotencyScopingArchitectureTests</c> enforces; moving that merge up here would have
/// bypassed the scoping, which is why <see cref="Resolve"/> exists alongside <see cref="From"/>
/// instead of replacing it. Financial has no normaliser and writes the key straight into a unique
/// index column, so it merges at the endpoint.
/// </para>
/// </summary>
public static class IdempotencyHeader
{
    /// <summary>
    /// The header's name, as the client sends it and the published contract documents it.
    /// </summary>
    public const string Name = "Idempotency-Key";

    /// <summary>
    /// The header's value, or null when the request carries none.
    /// <para>
    /// The first value where a client sends the header twice, and returned exactly as received:
    /// trimming here would change what Financial stores in an index column, and the modules that
    /// normalise trim it themselves.
    /// </para>
    /// </summary>
    public static string? From(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Request.Headers.TryGetValue(Name, out var values)
            ? values.FirstOrDefault()
            : null;
    }

    /// <summary>
    /// The header's key if it carries one, otherwise the body's.
    /// <para>
    /// For a module that has no normaliser to merge them later. Both are accepted because the app
    /// sends the body field, and accepting only the header would need a client release before a
    /// single conversion could carry a key.
    /// </para>
    /// </summary>
    public static string? Resolve(HttpContext context, string? body)
    {
        var header = From(context);

        return !string.IsNullOrWhiteSpace(header) ? header : body;
    }
}
