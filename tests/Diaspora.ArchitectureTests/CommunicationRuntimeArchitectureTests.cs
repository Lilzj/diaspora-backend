using System.Reflection;
using Diaspora.Modules.Notifications;
using Diaspora.Modules.Notifications.Extensibility.Realtime;
using Diaspora.Platform.Communication;
using Diaspora.Platform.Modules;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The boundary around the real-time runtime.
/// <para>
/// <b>The rule these tests exist to keep is one sentence: business modules publish events, and the
/// Communication Platform owns every way a message physically moves.</b> Real-time is the single
/// most tempting thing to breach it with. Pushing a live update from a command handler is four lines
/// and works immediately — and it puts a transport concern inside a vertical, makes that vertical
/// impossible to run in a worker process, and means the next platform-wide change to how sockets are
/// authorised has to be made in eleven places. It would also pass code review, because the four
/// lines look obviously correct.
/// </para>
/// <para>
/// Everything here is checked by dependency rather than by naming, because a reference is what
/// actually creates the coupling and a name is what somebody can trivially work around.
/// </para>
/// </summary>
public sealed class CommunicationRuntimeArchitectureTests
{
    private const string SignalRNamespace = "Microsoft.AspNetCore.SignalR";

    private static readonly Assembly Module = typeof(NotificationsModule).Assembly;

    /// <summary>
    /// Every module except the one that owns communication. Loaded by name so that a module added
    /// later and forgotten here is caught by <see cref="The_list_of_business_modules_is_complete"/>
    /// rather than quietly escaping the rule.
    /// </summary>
    private static readonly string[] BusinessModuleNames =
    [
        "Diaspora.Modules.Bookmarks",
        "Diaspora.Modules.Trust",
        "Diaspora.Modules.Location",
        "Diaspora.Modules.Media",
        "Diaspora.Modules.Community",
        "Diaspora.Modules.Providers",
        "Diaspora.Modules.Guides",
        "Diaspora.Modules.Jobs",
        "Diaspora.Modules.Marketplace",
        "Diaspora.Modules.Housing",
        "Diaspora.Modules.CommunityLife",
        "Diaspora.Modules.Recommendations",
        "Diaspora.Modules.Financial",

        // B21. It composes geographic intelligence out of two other modules' facts and publishes it
        // over HTTP. The register it closes proposed an `area:` realtime channel family and this
        // module claims none of it: a geographic condition is a fact it can answer, and whether that
        // becomes a socket write is the Communication Platform's decision.
        "Diaspora.Modules.GeographicIntelligence",

        // B13.2. It owns conversations and claims the conversation channel family, but it is still a
        // business module by the rule that matters here: it must not name the transport.
        "Diaspora.Modules.Conversations",

        // B18. The register proposed four realtime channels for it — campaign progress, poll results,
        // organization and membership — and the module claims none of them. It publishes integration
        // events and the Communication Platform decides whether any of those becomes a socket write,
        // which is exactly the rule this list exists to enforce.
        "Diaspora.Modules.CommunityProgramme",

        // B18.x. The Creator Platform publishes creator.status.changed, a verification decision, a
        // milestone completion, two challenge events, a grant decision, a qualified referral and an
        // awarded sign-up bonus — and decides nothing about how any of them reaches a member. Every
        // one of the eleven Phase 7 creator gaps that "should notify" does so by publishing a
        // contract B13 consumes, which is CREATOR-GAP-015's whole point.
        "Diaspora.Modules.Creator",

        // B19. The Analytics Platform reads the whole estate's published contracts and pushes
        // nothing to anybody. It is the module with the strongest reason to want a socket — a live
        // dashboard is the obvious next request — and the strongest reason not to have one: a
        // vertical that could write to a client directly is a vertical that has stopped asking the
        // Communication Platform whether it may.
        "Diaspora.Modules.Analytics",

        // B22. The Integration Foundation is the module with the most tempting reason to reach the
        // transport: a member watching a connection screen while an authorization completes in a
        // browser is exactly the case a socket is for. It claims none of it. Five lifecycle
        // contracts are published — connected, disconnected, expiring, reauthorization-required,
        // sync-failed — and whether any of them becomes a socket write is the Communication
        // Platform's decision, made from a rule this module contributes to B13's own catalogue.
        "Diaspora.Modules.Integrations",

        // B23. The Automation Platform decides *when* somebody should be told something, which makes
        // it the module a socket would be most useful to and the one it would be most dangerous in: a
        // vertical that could push to a client directly would be a member-authored rule with a
        // transport behind it. It claims none. Four contracts are published — a delivery request, a
        // settlement receipt, a completed run and a rule needing attention — and whether any of them
        // reaches a device is the Communication Platform's decision, made from rules this module
        // contributes to B13's own catalogue.
        "Diaspora.Modules.Automation"
    ];

    /// <summary>
    /// <b>The rule the whole change rests on.</b> A vertical that referenced SignalR would be a
    /// vertical that could push to a client directly — and the moment one does, "Communication owns
    /// delivery" is a sentence in a document rather than a property of the code.
    /// </summary>
    [Fact]
    public void No_business_module_depends_on_the_realtime_transport()
    {
        var offenders = new List<string>();

        foreach (var name in BusinessModuleNames)
        {
            var assembly = Assembly.Load(new AssemblyName(name));

            var result = Types.InAssembly(assembly)
                .Should()
                .NotHaveDependencyOn(SignalRNamespace)
                .GetResult();

            if (!result.IsSuccessful)
            {
                offenders.AddRange(result.FailingTypeNames ?? []);
            }
        }

        offenders.Should().BeEmpty(
            "a business module publishes an integration event and stops; the Communication Platform "
            + "decides how — and whether — that becomes a socket write. Offenders: "
            + string.Join(", ", offenders));
    }

    /// <summary>
    /// The list above is only a rule if it names every module. A vertical added later and not added
    /// here would be exempt from the check for as long as nobody noticed.
    /// </summary>
    [Fact]
    public void The_list_of_business_modules_is_complete()
    {
        var discovered = ModuleAssemblyScanner
            .FindImplementations<IPlatformModule>(ModuleAssemblyScanner.FindPlatformAssemblies())
            .Select(type => type.Assembly.GetName().Name)
            .Where(name => name is not null && name.StartsWith("Diaspora.Modules.", StringComparison.Ordinal))
            .Select(name => name!)
            .Distinct()
            .ToList();

        var expected = BusinessModuleNames
            .Append(Module.GetName().Name!)
            .ToList();

        discovered.Should().BeSubsetOf(
            expected,
            "a module the platform loads but this file does not name is a module exempt from the "
            + "no-SignalR rule");
    }

    /// <summary>
    /// <b>The transport is confined to two namespaces inside the module that owns it.</b> Everything
    /// else — the in-app channel's fan-out, presence, the typing relay, the health contributor —
    /// goes through <see cref="IRealtimeNotifier"/>, which is what makes swapping SignalR for a
    /// hosted service a one-file change rather than a phase.
    /// </summary>
    [Fact]
    public void Only_the_hub_and_its_transport_adapter_name_SignalR()
    {
        var permitted = new[]
        {
            "Diaspora.Modules.Notifications.Realtime",
            "Diaspora.Modules.Notifications.Infrastructure.Realtime",

            // Composition. AddSignalR is called from the module's own registration, which is the
            // whole point — Program.cs never learns real-time exists.
            "Diaspora.Modules.Notifications"
        };

        var offenders = Types.InAssembly(Module)
            .That()
            .HaveDependencyOn(SignalRNamespace)
            .GetTypes()
            .Where(type => !permitted.Contains(type.Namespace, StringComparer.Ordinal))
            .Select(type => type.FullName)
            .ToList();

        offenders.Should().BeEmpty(
            "the transport lives behind IRealtimeNotifier; a type outside the hub and its adapter "
            + "that names SignalR is one that would have to be rewritten to change transports. "
            + "Offenders: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// <b>The ports name no transport at all.</b> An extension seam that mentioned SignalR would
    /// make every implementer depend on it, which is the opposite of what a seam is for.
    /// </summary>
    [Fact]
    public void The_realtime_ports_name_no_transport()
    {
        var result = Types.InAssembly(Module)
            .That()
            .ResideInNamespace("Diaspora.Modules.Notifications.Extensibility.Realtime")
            .Should()
            .NotHaveDependencyOnAny(SignalRNamespace, "Microsoft.AspNetCore.Http")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "a port that named a transport would not be a port. Offenders: "
            + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// <b>Every implementation of the outbound port belongs to the Communication Platform.</b> That
    /// is the "Communication owns the runtime" claim expressed as a dependency rather than as a
    /// sentence: a notifier shipped by a vertical would be that vertical pushing to clients directly,
    /// through a port whose whole purpose is that it does not.
    /// </summary>
    [Fact]
    public void Every_realtime_notifier_belongs_to_the_communication_platform()
    {
        var implementations = ModuleAssemblyScanner
            .FindImplementations<IRealtimeNotifier>(ModuleAssemblyScanner.FindPlatformAssemblies())
            .ToList();

        implementations.Should().NotBeEmpty();
        implementations.Should().OnlyContain(
            type => type.Assembly == Module,
            "a notifier outside the Communication Platform is a module pushing to a client itself");
    }

    /// <summary>
    /// <b>Exactly one implementation carries a transport; the rest do nothing.</b> Two live
    /// transports would mean two answers to "did this member get told", differing by which the
    /// container happened to resolve — the failure the channel registry refuses at startup, for the
    /// same reason. The null object exists because a deployment with real-time switched off must
    /// still be able to resolve the port: <c>AddSignalR</c> is not called there, so the SignalR
    /// notifier's own dependency is absent and every in-app delivery would throw.
    /// </summary>
    [Fact]
    public void Exactly_one_realtime_notifier_carries_a_transport()
    {
        var withTransport = ModuleAssemblyScanner
            .FindImplementations<IRealtimeNotifier>(ModuleAssemblyScanner.FindPlatformAssemblies())
            .Where(type => Types.InAssembly(Module)
                .That().HaveName(type.Name)
                .Should().NotHaveDependencyOn(SignalRNamespace)
                .GetResult().IsSuccessful is false)
            .ToList();

        withTransport.Should().ContainSingle(
            "one door out to a client is what keeps the fan-out path single. Found: "
            + string.Join(", ", withTransport.Select(type => type.FullName)));
    }

    /// <summary>
    /// The four real-time seams, asserted to ship a default inside the Communication Platform.
    /// <para>
    /// A seam that quietly lost its shipped default would leave the container unable to build a hub,
    /// and the failure would arrive as a resolution error on the first connection rather than at
    /// boot.
    /// </para>
    /// <para>
    /// <b>B13.2 changed what "only" means for one of them, and the change is a correction rather
    /// than a relaxation.</b> This test previously required every implementation of all four seams
    /// to live in the Communication Platform. For <see cref="IRealtimeGroupAuthorizer"/> that
    /// contradicted the seam's own documentation, which says in as many words that "the vertical
    /// that eventually owns conversations registers its own implementation after this module" — so
    /// the test as written would have failed the moment the seam was used as designed. It now
    /// requires a shipped default <em>in</em> the Communication Platform for all four, and permits
    /// an additional group authorizer from the Conversations Platform and from nowhere else.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(typeof(IRealtimeConnectionRegistry))]
    [InlineData(typeof(IPresenceStore))]
    [InlineData(typeof(IRealtimeBackplane))]
    [InlineData(typeof(IRealtimeGroupAuthorizer))]
    public void Every_realtime_seam_ships_a_default(Type seam)
    {
        var implementations = ModuleAssemblyScanner.FindPlatformAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(seam.IsAssignableFrom)
            .ToList();

        implementations.Should().NotBeEmpty($"{seam.Name} must have a shipped default");

        implementations.Should().Contain(
            type => type.Assembly == Module,
            $"{seam.Name} must ship a default inside the Communication Platform, or a deployment "
            + "without the optional module cannot build a hub at all");

        // B13.3 narrowed this back. IRealtimeGroupAuthorizer used to permit an implementation from
        // the Conversations Platform, because that was the only way a vertical could own a channel:
        // register one and replace the shipped default. That is precisely the defect B13.3 removed —
        // a seam only one module can use — so the port is once again the Communication Platform's
        // alone, and a vertical contributes through IRealtimeChannelAuthorizer instead. An
        // implementation appearing in a vertical now means somebody has reintroduced the
        // single-winner arrangement.
        implementations
            .Select(type => type.Assembly.GetName().Name!)
            .Should()
            .OnlyContain(
                name => string.Equals(name, Module.GetName().Name!, StringComparison.Ordinal),
                $"only the Communication Platform may implement {seam.Name}. A vertical that wants a "
                + "channel claims a prefix on IRealtimeChannelAuthorizer, which composes; replacing "
                + "this port switches off every other module's channels with nothing logged");
    }

    /// <summary>
    /// <b>The defect B13.3 exists to make unrepeatable.</b>
    /// <para>
    /// Every real-time channel family is claimed by exactly one contributor, and the registry refuses
    /// two claiming one prefix at startup. This asserts the same thing statically, so the failure
    /// arrives in CI rather than as a host that will not boot — and, more importantly, so the reason
    /// is stated next to the rule.
    /// </para>
    /// <para>
    /// A prefix must end with the separator. <c>event</c> rather than <c>event:</c> also claims
    /// <c>eventual:</c>, which is the kind of overlap that surfaces only once a second module picks a
    /// name beginning with somebody else's word.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_realtime_channel_prefix_is_claimed_by_exactly_one_module()
    {
        var contributors = ModuleAssemblyScanner
            .FindImplementations<IRealtimeChannelAuthorizer>(
                ModuleAssemblyScanner.FindPlatformAssemblies())
            .Select(type => (Type: type, Instance: (IRealtimeChannelAuthorizer)
                System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type)))
            .ToList();

        contributors.Should().NotBeEmpty(
            "the member's own channel is contributed by the Communication Platform, so an empty "
            + "result means the seam has stopped being discovered and every join is refused");

        var prefixes = contributors.Select(entry => entry.Instance.ChannelPrefix).ToList();

        prefixes.Should().OnlyContain(
            prefix => prefix.EndsWith(':'),
            "a prefix without its separator also claims every longer word that starts with it");

        prefixes.Should().OnlyHaveUniqueItems(
            "two modules claiming one channel family is a startup failure, and picking whichever the "
            + "container ordered first would decide who can read a private thread by accident. "
            + "Found: " + string.Join(", ", prefixes));
    }

    /// <summary>
    /// <b>Community Life owns two channel families and holds no reference to the platform that
    /// carries them.</b>
    /// <para>
    /// The point of putting the channel seam in the kernel. Community Life registers at order 1100
    /// and the Communication Platform at 1200 — B13 consumes what B12 publishes — so an edge from
    /// B12 to B13 would be the first cycle in the graph B2.6 exists to keep acyclic. If this ever
    /// fails, the seam has been moved back into the Communication Platform and a module has been
    /// made to reach forward to reach it.
    /// </para>
    /// </summary>
    [Fact]
    public void A_vertical_that_owns_a_channel_family_does_not_reference_the_communication_platform()
    {
        var communityLife = Assembly.Load("Diaspora.Modules.CommunityLife");

        communityLife
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Should()
            .NotContain(
                Module.GetName().Name!,
                "Community Life registers before the Communication Platform, so a reference to it "
                + "would invert the module graph. Its channel authorizers implement a kernel seam");
    }

    /// <summary>
    /// <b>The Conversations Platform must register after the Communication Platform.</b>
    /// <para>
    /// Its group authorizer replaces the shipped own-group default by last-registration-wins, and
    /// registering first would leave the default in place. Nothing would report a fault: every
    /// conversation group would stay unjoinable and typing, read receipts and message fan-out would
    /// relay to nobody, exactly as they did before this module existed. The module order is the only
    /// thing standing between that and a working relay, so it is asserted rather than trusted.
    /// </para>
    /// </summary>
    [Fact]
    public void The_conversations_module_registers_after_the_communication_platform()
    {
        var modules = ModuleAssemblyScanner
            .CreateAll<IPlatformModule>(ModuleAssemblyScanner.FindPlatformAssemblies())
            .ToList();

        var communication = modules.Find(module => module.Name == "Notifications");
        var conversations = modules.Find(module => module.Name == "Conversations");

        communication.Should().NotBeNull();
        conversations.Should().NotBeNull();

        conversations!.Order.Should().BeGreaterThan(
            communication!.Order,
            "the conversation group authorizer only replaces the shipped default if it is "
            + "registered afterwards, and nothing at runtime would report that it had not");
    }

    /// <summary>
    /// <b>No vendor name in the real-time runtime either.</b> B13 established the rule for push
    /// channels; a hosted real-time service is the same class of dependency and arrives the same way
    /// — as an adapter registered against a port, never as a type in this assembly.
    /// </summary>
    [Fact]
    public void No_realtime_type_names_a_vendor()
    {
        string[] vendors = ["Pusher", "Ably", "Sendbird", "Stream", "Firebase", "Fcm", "Apns"];

        var offenders = Module.GetTypes()
            .Where(type => type.Namespace?.Contains("Realtime", StringComparison.Ordinal) == true)
            .Where(type => vendors.Any(vendor =>
                type.Name.Contains(vendor, StringComparison.OrdinalIgnoreCase)))
            .Select(type => type.FullName)
            .ToList();

        offenders.Should().BeEmpty(
            "a real-time vendor arrives as an adapter registered against IRealtimeNotifier. "
            + "Offenders: " + string.Join(", ", offenders));
    }
}
