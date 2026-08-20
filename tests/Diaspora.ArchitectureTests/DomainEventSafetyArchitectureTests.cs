using System.Reflection;
using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Events;
using Diaspora.Platform.Modules;
using Diaspora.SharedKernel.Primitives;
using FluentAssertions;
using MediatR;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// Settlement ownership and handler boundaries, asserted over the whole platform at once.
/// <para>
/// <b>The analyzer already checks each of these inside one compilation, and that is not the same
/// statement.</b> <c>DSP1002</c> can only see the module it is compiling; whether an integration
/// event is settled by three modules that each believe they own it is a question about the
/// assembly graph, and only something standing outside every module can ask it. So the analyzer
/// catches the mistake as it is typed and this catches the shape the analyzer is structurally
/// unable to see.
/// </para>
/// </summary>
public sealed class DomainEventSafetyArchitectureTests
{
    private static readonly IReadOnlyList<Assembly> Platform =
        ModuleAssemblyScanner.FindPlatformAssemblies();

    private static readonly IReadOnlyList<Type> Handlers = [.. Platform
        .SelectMany(Loadable)
        .Where(type => type is { IsAbstract: false, IsInterface: false })
        .Where(type => type.GetInterfaces().Any(IsNotificationHandler))];

    /// <summary>
    /// The sanity check the rest depend on. A scan that silently finds nothing is a suite that
    /// passes for the worst possible reason.
    /// </summary>
    [Fact]
    public void The_handler_scan_finds_the_platforms_handlers()
    {
        Handlers.Should().HaveCountGreaterThan(
            100, "the platform has well over a hundred event handlers across fifteen modules");
    }

    /// <summary>
    /// <b>A domain event is private to the module that raised it, and this is what makes that
    /// structural rather than conventional.</b> A handler in another assembly would be reading
    /// another module's internal facts — the exact substitutability B1.2 removed at the type level,
    /// reappearing through an assembly reference instead of through an inheritance chain.
    /// </summary>
    [Fact]
    public void No_module_handles_another_modules_domain_event()
    {
        var offenders = new List<string>();

        foreach (var handler in Handlers)
        {
            foreach (var handled in HandledEvents(handler, typeof(DomainEventNotification<>)))
            {
                if (ModuleOf(handled.Assembly) != ModuleOf(handler.Assembly))
                {
                    offenders.Add(
                        $"{handler.FullName} handles {handled.Name} from {handled.Assembly.GetName().Name}");
                }
            }
        }

        offenders.Should().BeEmpty(
            "a domain event never crosses an assembly boundary; publish an integration event " +
            "instead:\n" + string.Join("\n", offenders));
    }

    /// <summary>
    /// <b>One module, one consumer per published contract.</b> Fan-out across modules is the whole
    /// point of a contract; fan-out inside one module is ownership ambiguity — delivery is
    /// at-least-once, so two consumers means two chances to apply one consequence twice, in an
    /// order nothing guarantees.
    /// </summary>
    [Fact]
    public void No_module_consumes_one_integration_event_twice()
    {
        var duplicates = Handlers
            .SelectMany(handler => HandledEvents(handler, typeof(IntegrationEventNotification<>))
                .Select(handled => (Module: handler.Assembly.GetName().Name!, Event: handled, Handler: handler)))
            .GroupBy(entry => (entry.Module, entry.Event))
            .Where(group => group.Count() > 1)
            .Select(group =>
                $"{group.Key.Module} consumes {group.Key.Event.Name} in " +
                string.Join(" and ", group.Select(entry => entry.Handler.Name)))
            .ToList();

        duplicates.Should().BeEmpty(
            "one module owns one reaction to one published fact:\n" + string.Join("\n", duplicates));
    }

    /// <summary>
    /// <b>The two pipelines make opposite promises, so no type may serve both.</b> A domain event
    /// handler runs inline, inside the transaction that raised the event, exactly once, and may
    /// read uncommitted state. An integration consumer runs after commit, out of band, at least
    /// once, in its own scope. A type doing both is written against one set of assumptions and is
    /// wrong about the other half of itself.
    /// </summary>
    [Fact]
    public void No_handler_serves_both_pipelines()
    {
        var offenders = Handlers
            .Where(handler =>
                HandledEvents(handler, typeof(DomainEventNotification<>)).Count > 0 &&
                HandledEvents(handler, typeof(IntegrationEventNotification<>)).Count > 0)
            .Select(handler => handler.FullName!)
            .ToList();

        offenders.Should().BeEmpty(
            "split it: the two pipelines guarantee different things:\n" + string.Join("\n", offenders));
    }

    /// <summary>
    /// <b>The rule B7 arrived at, kept.</b> A handler that takes another handler makes settlement
    /// order implicit — it becomes whatever the call graph is that day — and hides which of the two
    /// owns the consequence. Shared behaviour goes in a service both of them take.
    /// </summary>
    [Fact]
    public void No_handler_depends_on_another_handler()
    {
        var offenders = new List<string>();

        foreach (var handler in Handlers)
        {
            foreach (var constructor in handler.GetConstructors())
            {
                foreach (var parameter in constructor.GetParameters())
                {
                    var forbidden =
                        parameter.ParameterType.GetInterfaces().Any(IsNotificationHandler) ||
                        parameter.ParameterType == typeof(IMediator) ||
                        parameter.ParameterType == typeof(ISender) ||
                        parameter.ParameterType == typeof(IPublisher);

                    if (forbidden)
                    {
                        offenders.Add($"{handler.Name} takes {parameter.ParameterType.Name}");
                    }
                }
            }
        }

        offenders.Should().BeEmpty(
            "extract the shared behaviour into a service:\n" + string.Join("\n", offenders));
    }

    /// <summary>
    /// <b>The producer side of integration-event ownership.</b> A module publishes contracts about
    /// its own facts. A contract living in another module's namespace segment would mean one module
    /// announcing what another one did — which reads to every consumer as authority the announcing
    /// module does not have.
    /// </summary>
    [Fact]
    public void Every_published_contract_lives_in_the_contracts_assembly()
    {
        var strays = Platform
            .Where(assembly => assembly.GetName().Name != "Diaspora.Contracts")
            .SelectMany(Loadable)
            .Where(type =>
                typeof(IIntegrationEvent).IsAssignableFrom(type) &&
                type is { IsAbstract: false, IsInterface: false })
            .Select(type => type.FullName!)
            .ToList();

        strays.Should().BeEmpty(
            "a published contract is frozen and shared, and lives where consumers can reach it " +
            "without referencing its producer:\n" + string.Join("\n", strays));
    }

    /// <summary>
    /// The B1.2 separation, from the other side: no type may satisfy both hierarchies. The analyzer
    /// reports this as an error at the keystroke; this catches a type arriving from a referenced
    /// assembly the analyzer never compiled.
    /// </summary>
    [Fact]
    public void No_type_is_both_kinds_of_event()
    {
        var hybrids = Platform
            .SelectMany(Loadable)
            .Where(type =>
                typeof(IDomainEvent).IsAssignableFrom(type) &&
                typeof(IIntegrationEvent).IsAssignableFrom(type))
            .Select(type => type.FullName!)
            .ToList();

        hybrids.Should().BeEmpty(
            "the hierarchies meet at IMessage and nowhere else:\n" + string.Join("\n", hybrids));
    }

    /// <summary>
    /// The module an assembly belongs to.
    /// <para>
    /// <b>Assembly is not module, and Identity is why.</b> Every vertical since B3 ships as one
    /// project, so for those two the words are interchangeable — but Identity predates that
    /// convention and is spread across the layered assemblies it was built in: its aggregates live
    /// in <c>Diaspora.Domain</c>, its handlers in <c>Diaspora.Application</c>, its controllers in
    /// <c>Diaspora.Api</c>. A rule that read the assembly name would report fifteen of its handlers
    /// as reaching across a boundary that does not exist between them.
    /// </para>
    /// </summary>
    private static string ModuleOf(Assembly assembly)
    {
        var name = assembly.GetName().Name ?? string.Empty;

        return name is "Diaspora.Domain" or "Diaspora.Application" or "Diaspora.Api"
            ? "Identity"
            : name;
    }

    private static bool IsNotificationHandler(Type contract) =>
        contract.IsGenericType &&
        contract.GetGenericTypeDefinition() == typeof(INotificationHandler<>);

    /// <summary>
    /// The events a handler serves through one of the two notification wrappers. Reads the closed
    /// interface list, so a handler three abstractions deep — Communication's consumers derive a
    /// base class of their own — still reports what it actually handles.
    /// </summary>
    private static List<Type> HandledEvents(Type handler, Type wrapper) =>
        [.. handler.GetInterfaces()
            .Where(IsNotificationHandler)
            .Select(contract => contract.GetGenericArguments()[0])
            .Where(notification =>
                notification.IsGenericType &&
                notification.GetGenericTypeDefinition() == wrapper)
            .Select(notification => notification.GetGenericArguments()[0])
            .Distinct()];

    private static IEnumerable<Type> Loadable(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type is not null)!;
        }
    }
}
