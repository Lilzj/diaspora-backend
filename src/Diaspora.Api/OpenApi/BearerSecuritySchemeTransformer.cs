using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Diaspora.Api.OpenApi;

/// <summary>
/// Declares the bearer scheme every authenticated operation is reached through, and makes it the
/// document's default — BSEC-011 and BBL-003.
/// </summary>
/// <remarks>
/// <para>
/// <b>The contract used to say nothing at all.</b> 936 paths, 1,092 operations, no
/// <c>components.securitySchemes</c>, no document-level <c>security</c>, and not one operation
/// declaring any. A client generated from it — an SDK, a Postman collection, a partner
/// integration, an agent reading the contract — constructs every request without an
/// <c>Authorization</c> header, and the document is the only published description of this API
/// because <c>MapOpenApi</c> is mapped only under <c>IsDevelopment()</c>.
/// </para>
/// <para>
/// <b>The document-level default is the half that fails closed.</b> A consumer that ignores
/// per-operation <c>security</c> — and a reader skimming the top of the file — sees that this API
/// requires a bearer token, which is true of roughly a thousand of its operations. The operation
/// transformer then names the exceptions. Were the default absent, an operation whose
/// <c>security</c> somehow went missing would read as public; with it, the same operation reads as
/// authenticated. Both halves point the same way on purpose.
/// </para>
/// <para>
/// <b>Why this does not also describe the refresh cookie.</b> Refresh is a single operation that
/// takes an <c>HttpOnly</c> cookie the browser attaches on its own, and a generated client cannot
/// usefully be told to set it. Describing it as a second scheme would add a security requirement
/// no consumer can satisfy by hand, to say something the cookie already does. The access token is
/// what a caller holds and chooses to send, so it is what the contract describes.
/// </para>
/// </remarks>
public sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    /// <summary>
    /// The component key the per-operation requirements reference.
    /// </summary>
    /// <remarks>
    /// Lower-case because it is the scheme name in the <c>Authorization</c> header and reads that
    /// way in generated clients. It is not an OpenAPI keyword — the name is arbitrary and only has
    /// to match between <c>components.securitySchemes</c> and every <c>security</c> entry, which is
    /// exactly why both sides take it from here.
    /// </remarks>
    public const string SchemeId = "bearer";

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??=
            new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);

        document.Components.SecuritySchemes[SchemeId] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description =
                "The access token issued by POST /api/v1/auth/login and rotated by "
                + "POST /api/v1/auth/refresh, sent as `Authorization: Bearer <token>`. "
                + "An operation declaring `security: []` needs no token; every other operation "
                + "does, whether the requirement is enforced on the route or by the request "
                + "pipeline behind it.",
        };

        // The default, which every operation then restates or overrides. See the class remarks for
        // why it is declared in both places.
        document.Security = [Requirement(document)];

        return Task.CompletedTask;
    }

    /// <summary>
    /// A requirement naming this document's bearer scheme, with no scopes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The scope list is empty because OpenAPI reserves it for <c>oauth2</c> and
    /// <c>openIdConnect</c>; for an HTTP scheme the specification requires it to be empty. This
    /// platform's permissions are not OAuth scopes and are not expressible here — what a token
    /// permits is answered by <c>AuthorizationBehavior</c> against the member's permissions, not by
    /// anything the caller can read off the contract.
    /// </para>
    /// <para>
    /// <paramref name="document"/> is the host the reference resolves against and is nullable
    /// because the operation transformer's context declares it so. A reference carries the scheme
    /// id either way and is resolved by that id when the document is serialised, by which point
    /// <see cref="TransformAsync"/> has registered the component — document transformers run after
    /// operation transformers, so the id is written before the thing it names exists, and the
    /// contract is consistent only once both have run. <c>The_bearer_scheme_is_declared_once</c>
    /// is what checks that held.
    /// </para>
    /// </remarks>
    internal static OpenApiSecurityRequirement Requirement(OpenApiDocument? document) =>
        new() { [new OpenApiSecuritySchemeReference(SchemeId, document)] = [] };
}
