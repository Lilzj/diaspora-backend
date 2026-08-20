using Diaspora.Platform.Http;
using Diaspora.SharedKernel.Results;
using Microsoft.AspNetCore.Mvc;

namespace Diaspora.Api.Common;

/// <summary>
/// Turns a handler's <see cref="Result"/> into an MVC response.
/// <para>
/// A thin adapter over <see cref="PlatformProblem"/> and the platform envelope — the same
/// types a module's minimal-API endpoint uses through <c>PlatformResults</c>. Sharing the
/// construction rather than copying it is what makes the API contract identical across the
/// host's controllers and every module's own routes.
/// </para>
/// </summary>
[ApiController]
public abstract class ApiController : ControllerBase
{
    protected IActionResult HandleResult(Result result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess
            ? Ok(new ApiResponse<object?>(null, ApiMeta.For(HttpContext)))
            : MapError(result.Error);
    }

    protected IActionResult HandleResult<T>(Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess
            ? Ok(new ApiResponse<T>(result.Value, ApiMeta.For(HttpContext)))
            : MapError(result.Error);
    }

    protected IActionResult HandleCreated<T>(
        Result<T> result,
        string? routeName = null,
        object? routeValues = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsFailure)
        {
            return MapError(result.Error);
        }

        var body = new ApiResponse<T>(result.Value, ApiMeta.For(HttpContext));

        return routeName is not null
            ? CreatedAtRoute(routeName, routeValues, body)
            : StatusCode(StatusCodes.Status201Created, body);
    }

    private ObjectResult MapError(Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.RateLimited => StatusCodes.Status429TooManyRequests,
            ErrorType.ServiceUnavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };

        var problem = PlatformProblem.Create(
            HttpContext, statusCode, error.Code, TitleFor(error.Type), error.Description);

        return StatusCode(statusCode, problem);
    }

    private static string TitleFor(ErrorType type) => type switch
    {
        ErrorType.Validation => "Validation failed",
        ErrorType.NotFound => "Not found",
        ErrorType.Conflict => "Conflict",
        ErrorType.Unauthorized => "Unauthorized",
        ErrorType.Forbidden => "Forbidden",
        ErrorType.RateLimited => "Too many requests",
        ErrorType.ServiceUnavailable => "Service unavailable",
        _ => "Unexpected error"
    };
}
