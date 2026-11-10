using System.Net;
using Diaspora.Domain.Abstractions;
using Diaspora.Platform.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diaspora.Api.Middleware;

/// <summary>
/// Turns anything that escapes a handler into the platform's one error shape.
/// <para>
/// Three B2 defects are fixed here rather than worked around. The response was written with
/// <c>WriteAsJsonAsync(value, options)</c>, whose overload overwrites the content type set
/// two lines above it — so every problem document went out as <c>application/json</c>.
/// There was no <c>HasStarted</c> guard, so an exception thrown mid-response replaced the
/// real failure with a second one. And <c>OperationCanceledException</c> fell into the
/// catch-all and became a logged 500, which on a mobile client means every backgrounded app
/// contributes to the error budget.
/// </para>
/// </summary>
public sealed class GlobalExceptionHandlingMiddleware
{
    /// <summary>
    /// Non-standard, and widely understood: nginx's code for "the client went away". Chosen
    /// over 400 so cancellations are trivially filterable in a dashboard.
    /// </summary>
    private const int ClientClosedRequest = 499;

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogDebug(
                "Request {Method} {Path} was cancelled by the client.",
                context.Request.Method,
                context.Request.Path);

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = ClientClosedRequest;
            }
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (status, code, title, detail) = Classify(exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "Unhandled exception on {Method} {Path} [CorrelationId: {CorrelationId}]",
                context.Request.Method,
                context.Request.Path,
                context.CorrelationId());
        }
        else
        {
            _logger.LogWarning(
                "Request refused with {StatusCode} {Code}: {Detail} [CorrelationId: {CorrelationId}]",
                status,
                code,
                detail,
                context.CorrelationId());
        }

        if (context.Response.HasStarted)
        {
            return;
        }

        // Validation is the one case carrying a structured body beyond the standard fields,
        // so it is built rather than written through the generic helper.
        if (exception is Application.Exceptions.ValidationException validation)
        {
            var errors = validation.Errors
                .GroupBy(error => error.Code, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.Description).ToArray(),
                    StringComparer.Ordinal);

            var problem = PlatformProblem.CreateValidation(
                context, errors, "One or more validation errors occurred.");

            context.Response.StatusCode = StatusCodes.Status400BadRequest;

            await context.Response.WriteAsJsonAsync(
                problem,
                options: null,
                contentType: PlatformProblem.ContentType,
                context.RequestAborted);

            return;
        }

        var extensions = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (exception is Application.Exceptions.StepUpRequiredException stepUp)
        {
            // What to ask for. Without it the client knows a code is needed and not which one, and
            // a grant minted for the wrong scope satisfies nothing — the refusal would repeat with
            // the member having already answered a challenge.
            extensions["stepUpScope"] = stepUp.Scope;
        }

        if (exception is Application.Exceptions.RateLimitException rateLimit)
        {
            extensions["retryAfter"] = rateLimit.RetryAfterSeconds;
            context.Response.Headers.RetryAfter =
                rateLimit.RetryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (_environment.IsDevelopment() && status >= StatusCodes.Status500InternalServerError)
        {
            extensions["exceptionType"] = exception.GetType().FullName;
            extensions["stackTrace"] = exception.StackTrace;
        }

        await PlatformProblem.WriteAsync(
            context, status, code, title, detail, extensions, context.RequestAborted);
    }

    private (int Status, string Code, string Title, string Detail) Classify(Exception exception) =>
        exception switch
        {
            // ── Request the server could not read ─────────────────────────────
            //
            // Minimal APIs and MVC controllers are two binding pipelines in one host,
            // and a client cannot tell which one it is addressing. MVC turned a body it
            // could not bind into a 400 with field-level errors; minimal APIs let
            // `BadHttpRequestException` escape to the catch-all below, so the same
            // mistake came back as 500 Platform.Unexpected — "Unexpected error" for a
            // request that was simply malformed, which tells the client nothing and
            // makes a client bug indistinguishable from a server fault in alerting.
            //
            // This is deliberately narrow. `BadHttpRequestException` is the framework's
            // own "the caller's request was not readable" signal and it carries the
            // status it wants — 400 for unbindable JSON, 413 for a body over the limit.
            // Honouring that status is not the same as turning every exception into a
            // 400: anything genuinely unexpected still falls through to 500.
            BadHttpRequestException ex =>
                (ex.StatusCode,
                 PlatformErrorCodes.Validation,
                 ex.StatusCode == StatusCodes.Status413PayloadTooLarge ? "Payload too large" : "Malformed request",
                 // The framework's message names the parameter it could not bind, which
                 // is the single most useful thing a client can be told here. It
                 // describes the request, never server state, so it is safe outside
                 // Development — unlike the inner JSON exception, which is not surfaced.
                 ex.Message),

            Application.Exceptions.NotFoundException ex =>
                ((int)HttpStatusCode.NotFound, PlatformErrorCodes.NotFound, "Not found", ex.Message),

            Application.Exceptions.ConflictException ex =>
                ((int)HttpStatusCode.Conflict, PlatformErrorCodes.Conflict, "Conflict", ex.Message),

            ConcurrencyConflictException =>
                ((int)HttpStatusCode.Conflict,
                 PlatformErrorCodes.ConcurrencyConflict,
                 "Conflict",
                 "The record was modified by another request. Reload and try again."),

            Application.Exceptions.UnauthorizedException ex =>
                ((int)HttpStatusCode.Unauthorized, PlatformErrorCodes.Unauthorized, "Unauthorized", ex.Message),

            // BSEC-007. A 403 the client is meant to act on rather than report: collect a code and
            // retry, where the generic Forbidden below means stop. Its own code is what makes that
            // possible, and the scope rides in an extension further down.
            //
            // A sibling of ForbiddenException, not a subclass, which is why this arm's position is
            // grouping rather than a requirement. Deriving from it would have made the order
            // load-bearing: move this below and every step-up refusal silently becomes
            // Platform.Auth.Forbidden again, with nothing failing to say so.
            Application.Exceptions.StepUpRequiredException ex =>
                ((int)HttpStatusCode.Forbidden,
                 PlatformErrorCodes.StepUpRequired,
                 "Confirmation required",
                 ex.Message),

            Application.Exceptions.ForbiddenException ex =>
                ((int)HttpStatusCode.Forbidden, PlatformErrorCodes.Forbidden, "Forbidden", ex.Message),

            Application.Exceptions.RateLimitException ex =>
                ((int)HttpStatusCode.TooManyRequests, PlatformErrorCodes.RateLimited, "Too many requests", ex.Message),

            // PSR-1 Final Certification. There were two types with this name — this one and
            // SharedKernel.Primitives.BusinessRuleViolationException, both mapped here, both named
            // by full namespace because neither could be written unqualified. The SharedKernel one
            // was the throwing half of the retired IBusinessRule primitive and is now
            // [Obsolete(error: true)] under DSP4005; nothing can construct it, so nothing can
            // reach the arm that used to catch it. This is the one supported type, and the rare
            // case it exists for is a rule failure that genuinely cannot be returned as a value.
            Application.Exceptions.BusinessRuleViolationException ex =>
                ((int)HttpStatusCode.UnprocessableEntity,
                 PlatformErrorCodes.BusinessRule,
                 "Business rule violation",
                 ex.Message),

            Application.Exceptions.InfrastructureException =>
                ((int)HttpStatusCode.ServiceUnavailable,
                 PlatformErrorCodes.ServiceUnavailable,
                 "Service unavailable",
                 "A dependent service is currently unavailable."),

            // B1.0.2. A transient database failure the platform declined to replay, because
            // replaying would have run against a change tracker that no longer held the work.
            // Nothing was committed, so 503 rather than 500: the honest instruction to the caller
            // is "this did not happen, try again", and a 500 would invite them to assume otherwise.
            TransientTransactionFailureException =>
                ((int)HttpStatusCode.ServiceUnavailable,
                 PlatformErrorCodes.ServiceUnavailable,
                 "Service unavailable",
                 "The write could not be completed because of a transient database failure. " +
                 "Nothing was changed; please try again."),

            _ => ((int)HttpStatusCode.InternalServerError,
                  PlatformErrorCodes.Unexpected,
                  "Unexpected error",
                  _environment.IsDevelopment() ? exception.Message : "An unexpected error occurred.")
        };
}
