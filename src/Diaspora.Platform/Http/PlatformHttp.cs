using Microsoft.AspNetCore.Http;

namespace Diaspora.Platform.Http;

/// <summary>Request-scoped values every layer of the HTTP stack reads.</summary>
public static class PlatformHttp
{
    public const string CorrelationIdHeader = "X-Correlation-Id";

    /// <summary>
    /// The correlation id for this request. Set by the correlation middleware; falls back
    /// to the trace identifier only for a request that somehow bypassed it.
    /// </summary>
    public static string CorrelationId(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var header = context.Request.Headers[CorrelationIdHeader].FirstOrDefault();
        return string.IsNullOrWhiteSpace(header) ? context.TraceIdentifier : header;
    }

    internal static string TraceId(this HttpContext context) =>
        System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
}

/// <summary>
/// The platform-wide success envelope.
/// <para>
/// One shape for every module, so a client parses the same thing everywhere. Failures use
/// RFC 9457 problem documents instead — see <see cref="PlatformProblem"/>.
/// </para>
/// </summary>
public sealed record ApiResponse<T>(T Data, ApiMeta Meta);

/// <summary>Per-response diagnostics, sourced from the request so they actually correlate.</summary>
public sealed record ApiMeta
{
    public required string RequestId { get; init; }

    public required string CorrelationId { get; init; }

    public required string Timestamp { get; init; }

    public static ApiMeta For(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new ApiMeta
        {
            RequestId = context.TraceId(),
            CorrelationId = context.CorrelationId(),
            Timestamp = DateTimeOffset.UtcNow.ToString("O")
        };
    }
}
