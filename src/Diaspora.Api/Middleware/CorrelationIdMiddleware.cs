using Diaspora.Platform.Http;
using Microsoft.AspNetCore.Http;

namespace Diaspora.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    // The header name is defined once, in the kernel, so the middleware that sets it and
    // the extension that reads it cannot disagree.
    private const string CorrelationIdHeader = PlatformHttp.CorrelationIdHeader;
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");

        context.Request.Headers[CorrelationIdHeader] = correlationId;
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
