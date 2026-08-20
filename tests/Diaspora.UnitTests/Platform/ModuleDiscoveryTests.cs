using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Contributors;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.UnitTests.Platform;

/// <summary>
/// Module discovery is what closes Program.cs to modification. These tests pin the
/// contract: modules are found by interface, ordered, and duplicate names are refused.
/// </summary>
public class ModuleDiscoveryTests
{
    [Fact]
    public void Discovery_finds_modules_across_the_supplied_assemblies()
    {
        var registry = new ModuleRegistry().DiscoverModules([typeof(ModuleDiscoveryTests).Assembly]);

        registry.Modules.Select(m => m.Name).Should().Contain(["Alpha", "Beta"]);
    }

    [Fact]
    public void Modules_are_ordered_by_their_declared_order()
    {
        var registry = new ModuleRegistry().DiscoverModules([typeof(ModuleDiscoveryTests).Assembly]);

        var names = registry.Modules.Select(m => m.Name).ToArray();

        // Alpha declares Order 1, Beta declares Order 2.
        names.ToList().IndexOf("Alpha").Should().BeLessThan(names.ToList().IndexOf("Beta"));
    }

    [Fact]
    public void Two_modules_with_the_same_name_are_refused()
    {
        var registry = new ModuleRegistry();
        registry.Register<AlphaModule>();

        // Registering the same name twice — here via the same type — is the collision the
        // guard exists to catch: two modules both claiming "Alpha" make the manifest and
        // the permission attribution ambiguous.
        var act = () => registry.Register<AlphaModule>();

        act.Should().Throw<InvalidOperationException>().WithMessage("*Alpha*");
    }

    [Fact]
    public void ConfigureServices_runs_every_discovered_module()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        new ModuleRegistry()
            .DiscoverModules([typeof(ModuleDiscoveryTests).Assembly])
            .ConfigureServices(services, configuration);

        services.Should().Contain(d => d.ServiceType == typeof(AlphaMarker));
        services.Should().Contain(d => d.ServiceType == typeof(BetaMarker));
    }

    // ─── Test modules, discovered by the scan above ─────────────────────────

    private sealed class AlphaMarker;

    private sealed class BetaMarker;

    public sealed class AlphaModule : IPlatformModule
    {
        public string Name => "Alpha";
        public string Version => "1.0.0";
        public int Order => 1;
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration) =>
            services.AddSingleton<AlphaMarker>();
    }

    public sealed class BetaModule : IPlatformModule
    {
        public string Name => "Beta";
        public string Version => "1.0.0";
        public int Order => 2;
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration) =>
            services.AddSingleton<BetaMarker>();
    }
}
