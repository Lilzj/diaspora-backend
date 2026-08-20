using Diaspora.Platform.Modules;

namespace Diaspora.Platform.Validation;

/// <summary>
/// What counts as a module name when a contributor names one.
/// <para>
/// <b>Not every contributor comes from a module, and the contributor model is deliberately fine
/// with that.</b> The outbox's operator surface, the transaction receipt's telemetry, the
/// application layer's activity source and B2.6's provisional catalogue all contribute through the
/// ordinary interfaces while being platform infrastructure — which the outbox's own contributor
/// says out loud: "infrastructure that needed a bespoke registration path would be evidence the
/// model had a hole in it".
/// </para>
/// <para>
/// So the check is not "is this a registered module" — that would report four correct contributors
/// on every boot, and a warning that fires on correct code is a warning people learn to scroll
/// past. It is "is this a name the platform recognises": a registered module, or one of the
/// platform-level namespaces the kernel and its adapters contribute under.
/// </para>
/// </summary>
internal static class KnownModules
{
    /// <summary>
    /// Prefixes reserved for contributions that are the platform rather than a module. Matched as
    /// prefixes so <c>Platform.Outbox</c> and <c>Platform.Transactions</c> are recognised without
    /// each new piece of kernel infrastructure needing a line here.
    /// </summary>
    private static readonly string[] PlatformPrefixes = ["Platform", "Application", "Infrastructure"];

    public static IReadOnlySet<string> Names(ModuleRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        return new HashSet<string>(
            registry.Modules.Select(registration => registration.Name),
            StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsRecognised(string? declared, IReadOnlySet<string> moduleNames)
    {
        ArgumentNullException.ThrowIfNull(moduleNames);

        if (string.IsNullOrWhiteSpace(declared))
        {
            return false;
        }

        return moduleNames.Contains(declared) ||
            PlatformPrefixes.Any(prefix =>
                declared.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
