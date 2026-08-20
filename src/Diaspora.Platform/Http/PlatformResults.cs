using Diaspora.SharedKernel.Results;
using Microsoft.AspNetCore.Http;

namespace Diaspora.Platform.Http;

/// <summary>
/// Maps a handler's <see cref="Result"/> to a minimal-API <see cref="IResult"/>, in the
/// platform's envelope and problem shape.
/// <para>
/// This is the minimal-API counterpart of the host's <c>ApiController.HandleResult</c>, and
/// it deliberately shares <see cref="PlatformProblem"/> with it. A module endpoint calls
/// this and gets byte-for-byte the same success envelope and error document a controller
/// would produce — so a module author never reimplements the API contract, and cannot drift
/// from it.
/// </para>
/// </summary>
public static class PlatformResults
{
    public static IResult Ok<T>(HttpContext context, Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess
            ? TypedResults.Ok(new ApiResponse<T>(result.Value, ApiMeta.For(context)))
            : Problem(context, result.Error);
    }

    public static IResult Ok(HttpContext context, Result result)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess
            ? TypedResults.Ok(new ApiResponse<object?>(null, ApiMeta.For(context)))
            : Problem(context, result.Error);
    }

    public static IResult Created<T>(HttpContext context, Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsFailure)
        {
            return Problem(context, result.Error);
        }

        return TypedResults.Created(
            (string?)null, new ApiResponse<T>(result.Value, ApiMeta.For(context)));
    }

    private static IResult Problem(HttpContext context, Error error)
    {
        var statusCode = StatusCodeFor(error.Type);
        var problem = PlatformProblem.Create(context, statusCode, error.Code, TitleFor(error.Type), error.Description);

        return Microsoft.AspNetCore.Http.Results.Problem(problem);
    }

    private static int StatusCodeFor(ErrorType type) => type switch
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
