namespace Diaspora.Platform.Modules;

public sealed record ModuleRegistration(string Name, string Version, IPlatformModule Module);
