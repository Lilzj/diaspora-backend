namespace Diaspora.Platform.Modules;

public sealed class PlatformManifest
{
    private readonly ModuleRegistry _registry;

    public PlatformManifest(ModuleRegistry registry)
    {
        _registry = registry;
    }

    public IReadOnlyList<ModuleInfo> GetRegisteredModules() =>
        _registry.Modules
            .Select(m => new ModuleInfo(m.Name, m.Version))
            .ToList()
            .AsReadOnly();
}
