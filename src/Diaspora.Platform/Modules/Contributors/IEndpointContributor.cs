using Microsoft.AspNetCore.Routing;

namespace Diaspora.Platform.Modules.Contributors;

/// <summary>
/// Implemented by a module to map its own HTTP endpoints.
/// <para>
/// Controllers discovered by MVC's application-part scanning remain fully supported —
/// Identity uses them. This exists for modules that prefer minimal APIs, and, more
/// importantly, so that a module living in its own assembly can contribute routes without
/// the host project referencing its controllers or its request types.
/// </para>
/// <para>
/// This is the one contributor that pulls ASP.NET Core into the kernel's surface. The
/// alternative — a separate <c>Diaspora.Platform.Web</c> assembly holding a single
/// interface — buys nothing for a platform whose only host is a web API, and costs every
/// module an extra reference.
/// </para>
/// </summary>
public interface IEndpointContributor
{
    /// <summary>The owning module's name.</summary>
    string Module { get; }

    /// <summary>
    /// Order relative to other endpoint contributors. Only matters when two modules map
    /// overlapping route patterns, which they should not.
    /// </summary>
    int Order => 0;

    /// <summary>
    /// Maps this module's routes. Called once, after authentication and authorisation are
    /// in the pipeline, with the host's top-level route builder.
    /// </summary>
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
