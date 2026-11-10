using Diaspora.Api.RateLimiting;
using Diaspora.Platform.Modules.Contributors;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.Api.Hosting;

/// <summary>
/// Maps every module's minimal-API endpoints.
/// </summary>
public static class ModuleEndpointRegistration
{
    public static IEndpointRouteBuilder MapModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // Contributors map into a group rather than onto the root builder, for one reason: a
        // transport budget. An empty prefix changes no route — every module still owns its own
        // path — but it gives the host one place to say what every module's endpoints get when
        // the module says nothing, which is what twenty-one verticals had been saying about rate
        // limiting. See RateLimitCoverage; a module may still name its own policy or opt out, and
        // either choice wins over this default.
        var group = endpoints
            .MapGroup(string.Empty)
            .WithPlatformRateLimitDefault();

        var contributors = endpoints.ServiceProvider
            .GetServices<IEndpointContributor>()
            .OrderBy(contributor => contributor.Order)
            .ThenBy(contributor => contributor.Module, StringComparer.Ordinal);

        foreach (var contributor in contributors)
        {
            contributor.MapEndpoints(group);
        }

        return endpoints;
    }
}
