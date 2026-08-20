using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.Platform.Modules;

public interface IPlatformModule
{
    string Name { get; }
    string Version { get; }
    int Order => 0;
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);
}
