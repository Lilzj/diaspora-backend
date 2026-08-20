using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Diaspora.Contracts.Events;
using Diaspora.Platform.Modules;

namespace Diaspora.Platform.Messaging.Outbox.Serialization;

/// <summary>
/// Builds the contract catalogue by scanning the platform's assemblies once at startup.
/// <para>
/// Discovery rather than registration, for the same reason the permission and feature
/// catalogues are discovered: a module that has to remember to register its events will
/// eventually forget, and the failure surfaces as a dead-lettered message in production
/// rather than a compile error.
/// </para>
/// <para>
/// <b>Name collisions fail the build-up, not the first message.</b> Two types claiming the same
/// contract name and version is a mistake that silently routes one module's events into
/// another's deserializer. The constructor throws, the readiness check fails, and the
/// deployment stops — which is the cheapest moment for this to be discovered.
/// </para>
/// </summary>
public sealed class IntegrationEventTypeResolver : IIntegrationEventTypeResolver
{
    private readonly Dictionary<ContractKey, IntegrationEventContract> _byContract;
    private readonly ConcurrentDictionary<Type, IntegrationEventContract> _byType;

    public IntegrationEventTypeResolver(IEnumerable<Assembly>? assemblies = null)
    {
        var scanned = assemblies?.ToArray() ?? ModuleAssemblyScanner.FindPlatformAssemblies().ToArray();

        _byType = new ConcurrentDictionary<Type, IntegrationEventContract>();
        _byContract = new Dictionary<ContractKey, IntegrationEventContract>();

        foreach (var type in Discover(scanned))
        {
            var contract = Describe(type);
            var key = new ContractKey(contract.Name, contract.Version);

            if (_byContract.TryGetValue(key, out var existing))
            {
                throw new InvalidOperationException(
                    $"Integration event contract '{contract.Name}' v{contract.Version} is claimed by " +
                    $"both '{existing.ClrType.FullName}' and '{type.FullName}'. A contract name " +
                    "identifies exactly one payload shape; give one of them an explicit " +
                    $"[{nameof(IntegrationEventContractAttribute)}] name or version.");
            }

            _byContract[key] = contract;
            _byType[type] = contract;
        }

        Contracts = _byContract.Values.OrderBy(c => c.Name, StringComparer.Ordinal).ToArray();
    }

    public IReadOnlyCollection<IntegrationEventContract> Contracts { get; }

    public IntegrationEventContract ContractFor(IIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        return ContractFor(integrationEvent.GetType());
    }

    public IntegrationEventContract ContractFor(Type clrType)
    {
        ArgumentNullException.ThrowIfNull(clrType);

        // Cached on miss as well as hit: a type discovered late — a test double, a type from an
        // assembly loaded after startup — still resolves, and still resolves only once.
        return _byType.GetOrAdd(clrType, static type =>
        {
            if (!typeof(IIntegrationEvent).IsAssignableFrom(type))
            {
                throw new ArgumentException(
                    $"'{type.FullName}' is not an integration event.", nameof(clrType));
            }

            return Describe(type);
        });
    }

    public bool TryResolve(
        string name, int version, [NotNullWhen(true)] out IntegrationEventContract? contract)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return _byContract.TryGetValue(new ContractKey(name, version), out contract);
    }

    public string ModuleFor(Type clrType) => ContractFor(clrType).Module;

    public bool IsKnown(Type clrType)
    {
        ArgumentNullException.ThrowIfNull(clrType);
        return typeof(IIntegrationEvent).IsAssignableFrom(clrType);
    }

    private static IEnumerable<Type> Discover(IReadOnlyList<Assembly> assemblies) =>
        ModuleAssemblyScanner.FindImplementations<IIntegrationEvent>(assemblies)
            .Where(type => type is { IsAbstract: false, IsGenericTypeDefinition: false });

    private static IntegrationEventContract Describe(Type type)
    {
        var attribute = type.GetCustomAttribute<IntegrationEventContractAttribute>(inherit: false);

        var name = attribute?.Name ?? DefaultName(type);
        var version = attribute?.Version ?? 1;

        return new IntegrationEventContract(name, version, type, DeriveModule(type));
    }

    /// <summary>
    /// The namespace-qualified type name, without the assembly.
    /// <para>
    /// <c>FullName</c> rather than <c>AssemblyQualifiedName</c> is the whole point: it survives
    /// an assembly version bump, a strong-name change and a repackaging, and it breaks only on
    /// a rename or a namespace move — both of which are contract changes that ought to break.
    /// </para>
    /// </summary>
    private static string DefaultName(Type type) => type.FullName ?? type.Name;

    /// <summary>
    /// Derives the owning module from the contract namespace.
    /// <para>
    /// <c>Diaspora.Contracts.Providers.V1.Events.X</c> yields <c>Providers</c>. A convention
    /// rather than a declaration, because every contract in the platform already follows it and
    /// asking each module to restate it would be six edits for a string they already encoded in
    /// their folder structure.
    /// </para>
    /// </summary>
    private static string DeriveModule(Type type)
    {
        const string ContractsRoot = "Diaspora.Contracts.";

        var ns = type.Namespace;

        if (string.IsNullOrEmpty(ns))
        {
            return "Unknown";
        }

        if (ns.StartsWith(ContractsRoot, StringComparison.Ordinal))
        {
            var remainder = ns[ContractsRoot.Length..];
            var separator = remainder.IndexOf('.', StringComparison.Ordinal);
            return separator < 0 ? remainder : remainder[..separator];
        }

        // A module that declares an event outside the shared contracts assembly still gets a
        // sensible name: Diaspora.Modules.Community.X → Community.
        var segments = ns.Split('.');
        return segments.Length >= 3 && string.Equals(segments[1], "Modules", StringComparison.Ordinal)
            ? segments[2]
            : segments[^1];
    }

    private readonly record struct ContractKey(string Name, int Version);
}
