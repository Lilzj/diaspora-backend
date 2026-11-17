using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Diaspora.Api.OpenApi;

/// <summary>
/// Gives every operation an explicit answer to "does a caller need a token for this" — BSEC-011
/// and BBL-003.
/// </summary>
/// <remarks>
/// <para>
/// <b>An absent <c>security</c> is not an answer.</b> It means "inherit the document default",
/// which is indistinguishable from nobody having thought about it — the same ambiguity
/// <see cref="Diaspora.Application.Abstractions.AllowAnonymousAttribute"/> exists to remove one
/// layer down. Every operation here declares its own, so the contract states 1,092 decisions
/// rather than one default and 1,092 silences.
/// </para>
/// <para>
/// <b>Unknown resolves to "token required", which is the opposite of what the rate limiter
/// does.</b> <c>RateLimitCoverage</c> treats an endpoint it cannot classify as anonymous, and is
/// right to: the cost of being wrong there is a member metered at the tighter budget. A contract
/// is asymmetric the other way. Over-declaring makes a client send a token it did not need, and
/// the request succeeds. Under-declaring makes it send none to a protected operation, and the
/// request fails — after the contract has told its reader the operation was public.
/// </para>
/// <para>
/// <b>That asymmetry is not hypothetical here.</b> 478 of this host's 1,100 endpoints declare no
/// authorization on the route at all; they are enforced on the request type by
/// <c>AuthorizationBehavior</c>, and <c>EndpointsWithoutRouteAuthorization.txt</c> pins the set by
/// name. Financial's 112 endpoints and Community's 100 are in it. A transformer that emitted
/// <c>security: []</c> for "no <c>IAuthorizeData</c> on the endpoint" — the obvious reading, and
/// the one the audit's remediation suggested — would publish
/// <c>DELETE /api/v1/financial/payment-methods/{paymentMethodId}</c> as a public operation. That
/// is worse than the silence it replaced: silence is uninformative, and this would be wrong.
/// </para>
/// <para>
/// <b>So only an explicit <c>IAllowAnonymous</c> produces <c>security: []</c>.</b> A route that
/// means to be public says so, and saying so is cheap — it is one call, it is what the
/// authorisation middleware already reads, and it is the same declaration
/// <c>RouteAuthorizationCoverageTests</c> and <c>RateLimitCoverage</c> consult. The consequence
/// worth knowing: an endpoint that is genuinely public but declares nothing on its route is
/// published as requiring a token. That over-declaration is visible in the contract, costs a
/// caller one unnecessary header, and is fixed by the route saying what it means.
/// </para>
/// </remarks>
public sealed class EndpointSecurityOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        operation.Security = IsPublic(context)
            ? []
            : [BearerSecuritySchemeTransformer.Requirement(context.Document)];

        return Task.CompletedTask;
    }

    /// <summary>
    /// Whether this operation's endpoint declares that it needs no authenticated caller.
    /// </summary>
    /// <remarks>
    /// <c>IAllowAnonymous</c> is read on its own rather than alongside <c>IAuthorizeData</c>,
    /// because it is the piece that carries intent. ASP.NET resolves the pair the same way the
    /// middleware does — the last <c>IAllowAnonymous</c> in the metadata wins over any
    /// authorisation declared above it, which is how a public route inside an authorised group
    /// works — so a route carrying both is public, and a route carrying neither has told this
    /// transformer nothing and takes the default above.
    /// </remarks>
    private static bool IsPublic(OpenApiOperationTransformerContext context) =>
        context.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any();
}
