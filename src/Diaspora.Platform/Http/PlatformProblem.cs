using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Diaspora.Platform.Http;

/// <summary>
/// Builds and writes the one error shape the whole platform emits.
/// <para>
/// The canonical implementation lives in the kernel, not in the host, precisely so a module
/// cannot produce a different one. The host's controllers, the exception middleware and a
/// module's minimal-API endpoints all route through here, which is what makes "every future
/// module inherits identical API behaviour" true by construction rather than by review.
/// </para>
/// </summary>
public static class PlatformProblem
{
    public const string ContentType = "application/problem+json";

    private const string TypeBaseUri = "https://diaspora.app/errors/";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static ProblemDetails Create(
        HttpContext context,
        int statusCode,
        string code,
        string title,
        string? detail,
        IDictionary<string, object?>? extensions = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path,
            Type = TypeBaseUri + code.ToLowerInvariant()
        };

        Enrich(problem, context, code, extensions);
        return problem;
    }

    public static ValidationProblemDetails CreateValidation(
        HttpContext context,
        IDictionary<string, string[]> errors,
        string detail)
    {
        ArgumentNullException.ThrowIfNull(context);

        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
            Detail = detail,
            Instance = context.Request.Path,
            Type = TypeBaseUri + PlatformErrorCodes.Validation.ToLowerInvariant()
        };

        Enrich(problem, context, PlatformErrorCodes.Validation, extensions: null);
        return problem;
    }

    /// <summary>
    /// Writes a problem document straight to the response, for the middleware and endpoint
    /// paths that have no <c>IActionResult</c> to return.
    /// </summary>
    public static async Task WriteAsync(
        HttpContext context,
        int statusCode,
        string code,
        string title,
        string? detail,
        IDictionary<string, object?>? extensions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Nothing useful can be said once the response has started, and trying throws —
        // which would lose the original failure behind an unrelated one.
        if (context.Response.HasStarted)
        {
            return;
        }

        var problem = Create(context, statusCode, code, title, detail, extensions);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = ContentType;

        await context.Response.WriteAsJsonAsync(
            problem, SerializerOptions, ContentType, cancellationToken);
    }

    private static void Enrich(
        ProblemDetails problem,
        HttpContext context,
        string code,
        IDictionary<string, object?>? extensions)
    {
        problem.Extensions["code"] = code;
        problem.Extensions["correlationId"] = context.CorrelationId();
        problem.Extensions["traceId"] = context.TraceId();
        problem.Extensions["timestamp"] = DateTimeOffset.UtcNow.ToString("O");

        if (extensions is null)
        {
            return;
        }

        foreach (var (key, value) in extensions)
        {
            problem.Extensions[key] = value;
        }
    }
}
