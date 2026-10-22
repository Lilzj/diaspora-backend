using Microsoft.AspNetCore.Http;

namespace Diaspora.Modules.Financial.Api;

/// <summary>
/// Reads the <c>Idempotency-Key</c> header.
/// <para>
/// The header wins where a body field is also present, the rule the Creator and Programme modules
/// already apply. Both are accepted because the app sends the body field, and accepting only the
/// header would need a client release before a single conversion could carry a key.
/// </para>
/// </summary>
internal static class IdempotencyHeader
{
    private const string Name = "Idempotency-Key";

    /// <summary>The header's key if it carries one, otherwise the body's.</summary>
    public static string? Resolve(HttpContext context, string? body)
    {
        ArgumentNullException.ThrowIfNull(context);

        var header = context.Request.Headers.TryGetValue(Name, out var values)
            ? values.FirstOrDefault()
            : null;

        return !string.IsNullOrWhiteSpace(header) ? header : body;
    }
}
