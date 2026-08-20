using Microsoft.AspNetCore.Http;

namespace Diaspora.Modules.CommunityProgramme.Api;

/// <summary>
/// Reads the <c>Idempotency-Key</c> header.
/// <para>
/// <b>Both the header and the body field are accepted, and the header wins.</b> The register asks for
/// "an <c>Idempotency-Key</c> header (or body field)"; the client sends the body field on every Phase
/// 12 mutation, so accepting only the header would have meant a client release before a single
/// application could be submitted. Reading it in one place means no endpoint spells the header
/// differently.
/// </para>
/// </summary>
internal static class IdempotencyHeader
{
    public const string Name = "Idempotency-Key";

    public static string? From(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Request.Headers.TryGetValue(Name, out var values)
            ? values.FirstOrDefault()
            : null;
    }
}
