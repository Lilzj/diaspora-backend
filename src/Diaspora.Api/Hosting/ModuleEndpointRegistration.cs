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

        var contributors = endpoints.ServiceProvider
            .GetServices<IEndpointContributor>()
            .OrderBy(contributor => contributor.Order)
            .ThenBy(contributor => contributor.Module, StringComparer.Ordinal);

        foreach (var contributor in contributors)
        {
            contributor.MapEndpoints(endpoints);
        }

        return endpoints;
    }
}
