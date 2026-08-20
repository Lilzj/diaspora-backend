using Microsoft.AspNetCore.Http;

namespace Diaspora.Modules.Creator.Api;

/// <summary>
/// Reads the <c>Idempotency-Key</c> header.
/// <para>
/// The header wins where a body field is also present, which is the rule
/// <c>CreatorIdempotency.Normalise</c> applies. Both are accepted because the client sends the body
/// field on every Phase 7 mutation, and accepting only the header would have meant a client release
/// before a single milestone could be claimed.
/// </para>
/// </summary>
internal static class IdempotencyHeader
{
    private const string Name = "Idempotency-Key";

    public static string? From(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Request.Headers.TryGetValue(Name, out var values)
            ? values.FirstOrDefault()
            : null;
    }
}
