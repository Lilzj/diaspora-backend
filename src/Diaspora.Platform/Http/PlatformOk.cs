using System.Net.Mime;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;

namespace Diaspora.Platform.Http;

/// <summary>
/// What <see cref="PlatformResults.Ok{T}"/> returns: the platform envelope, plus the one thing an
/// <see cref="IResult"/> cannot carry — the type of the body inside it.
/// <para>
/// <b>Why this type exists.</b> Until RMD-003 the emitted OpenAPI document described 82 of its
/// 1,093 2xx responses. The reason on record was that minimal-API endpoints return
/// <see cref="IResult"/>, so the generator has no return type to read. That is a symptom. The
/// cause is that <c>[ProducesResponseType]</c> was written 200 times in <c>Diaspora.Api</c> — the
/// host's MVC controllers, which return <c>Task&lt;IActionResult&gt;</c> and are just as untyped —
/// and zero times across the 29 module projects. The document described a body wherever somebody
/// typed the type by hand, and nowhere else.
/// </para>
/// <para>
/// <b>Why it needs no endpoint to change.</b> A minimal-API handler's metadata is populated from
/// the delegate's <i>return type</i>, and C# infers that from the lambda body. A handler that ends
/// in <c>PlatformResults.Ok(context, await sender.Send(query, ct))</c> infers
/// <c>Task&lt;PlatformOk&lt;T&gt;&gt;</c> the moment <c>Ok</c> stops returning <see cref="IResult"/>,
/// carrying <c>T</c> all the way from the query's own <c>IQuery&lt;T&gt;</c>. The framework then
/// calls <see cref="PopulateMetadata"/> because the return type implements
/// <see cref="IEndpointMetadataProvider"/>. 970 of the platform's 972 <c>Map*</c> sites go through
/// two methods on <see cref="PlatformResults"/>, so one type reaches nearly all of them.
/// </para>
/// <para>
/// <b>Only the success body is declared, deliberately.</b> <see cref="PlatformResults"/> can also
/// answer 400, 401, 403, 404, 409, 429 and 503, always as the same <c>ProblemDetails</c> shape.
/// Declaring all seven on every endpoint would multiply the document several times over to repeat
/// one schema that is already described once, and the finding this closes is about response
/// <i>bodies</i> being unnamed. The problem shape is stated on the controllers that declare it and
/// in <see cref="PlatformProblem"/>.
/// </para>
/// </summary>
/// <typeparam name="T">The payload inside the envelope — what <c>ApiResponse&lt;T&gt;.Data</c> holds.</typeparam>
public sealed class PlatformOk<T> : IResult, IEndpointMetadataProvider
{
    private readonly IResult _inner;

    internal PlatformOk(IResult inner) => _inner = inner;

    /// <summary>Writes whichever result <see cref="PlatformResults"/> chose — the envelope or the problem.</summary>
    public Task ExecuteAsync(HttpContext httpContext) => _inner.ExecuteAsync(httpContext);

    /// <summary>Declares the 200 body so the generator does not have to infer it.</summary>
    public static void PopulateMetadata(MethodInfo method, EndpointBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Metadata.Add(new ProducesResponseTypeMetadata(
            StatusCodes.Status200OK, typeof(ApiResponse<T>), [MediaTypeNames.Application.Json]));
    }
}

/// <summary>
/// What <see cref="PlatformResults.Created{T}"/> returns. <see cref="PlatformOk{T}"/> for 201,
/// and the same reasoning applies in full.
/// <para>
/// Worth knowing why this one mattered separately: without it the 89 <c>Created</c> endpoints did
/// not merely go undescribed, they were reported as <b>200</b>. A generator with nothing to read
/// emits a default success response, and the default is 200 — so the document stated the wrong
/// status code for every one of them, which is a worse failure than silence.
/// </para>
/// </summary>
/// <typeparam name="T">The payload inside the envelope.</typeparam>
public sealed class PlatformCreated<T> : IResult, IEndpointMetadataProvider
{
    private readonly IResult _inner;

    internal PlatformCreated(IResult inner) => _inner = inner;

    /// <summary>Writes whichever result <see cref="PlatformResults"/> chose — the envelope or the problem.</summary>
    public Task ExecuteAsync(HttpContext httpContext) => _inner.ExecuteAsync(httpContext);

    /// <summary>Declares the 201 body so the generator does not have to infer it.</summary>
    public static void PopulateMetadata(MethodInfo method, EndpointBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Metadata.Add(new ProducesResponseTypeMetadata(
            StatusCodes.Status201Created, typeof(ApiResponse<T>), [MediaTypeNames.Application.Json]));
    }
}
