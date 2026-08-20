using System.Reflection;
using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Events;
using Diaspora.Contracts.Notifications.V1.Events;
using Diaspora.Modules.Notifications;
using Diaspora.Modules.Notifications.Application.Consumers;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.Modules.Notifications.Domain.Templates;
using Diaspora.Modules.Notifications.Extensibility.Channels;
using Diaspora.Platform.Modules.Contributors;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The boundaries the Communication Platform must not cross, enforced rather than documented.
/// <para>
/// Each of these is true on the day it is written and quietly false two releases later unless
/// something checks. The reference test is the one that earns its keep: a single
/// <c>ProjectReference</c> added to render one extra field would undo the module's central claim, and
/// it would pass code review as a convenience.
/// </para>
/// </summary>
public class NotificationArchitectureTests
{
    private static readonly Assembly Module = typeof(NotificationsModule).Assembly;

    /// <summary>
    /// <b>Communication owns no business data, and this is how that is checked.</b> It references no
    /// vertical at all — not Jobs, not Housing, not Trust, not even Identity. Every notification is
    /// assembled from a published contract's payload; if the payload is not enough, the fix is to
    /// enrich the contract, never to reach into the module.
    /// </summary>
    [Fact]
    public void The_module_references_no_other_module()
    {
        var result = Types.InAssembly(Module)
            .Should()
            .NotHaveDependencyOnAny(
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
                "Diaspora.Modules.Bookmarks",
                "Diaspora.Domain.Identity")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the Communication Platform consumes published contracts and nothing else. Offenders: "
            + Describe(result));
    }

    /// <summary>
    /// <b>Only integration events cross a module boundary.</b> Every consumer derives from
    /// <see cref="ModuleEventConsumer{T}"/>, which derives from <c>IntegrationEventHandler</c> — so a
    /// consumer written against the domain pipeline is a compile error rather than a silent bypass of
    /// the outbox.
    /// </summary>
    [Fact]
    public void Every_cross_module_consumer_is_an_integration_event_handler()
    {
        var offenders = Module.GetTypes()
            .Where(type => type.Namespace?.Contains(".Application.Consumers", StringComparison.Ordinal) == true)
            .Where(type => type is { IsAbstract: false, IsInterface: false })

            // Iterator and async methods compile to nested state-machine types in the same
            // namespace. They are not consumers, and a rule that counted them would fail on every
            // consumer that uses yield return — which is all of them.
            .Where(type => !IsCompilerGenerated(type))
            .Where(type => !IsIntegrationEventConsumer(type))
            .ToList();

        offenders.Should().BeEmpty(
            "a consumer of another module's fact must ride the integration pipeline. Offenders: "
            + string.Join(", ", offenders.Select(type => type.FullName)));
    }

    /// <summary>
    /// <b>Business modules never create notifications directly.</b> The aggregate's factory is the
    /// only way to make one, and the dispatcher is the only caller of it — so there is no route from
    /// a vertical into this module's write path at all. Checked from the other side: nothing outside
    /// the dispatch namespace implements the dispatcher.
    /// </summary>
    [Fact]
    public void Only_the_dispatcher_creates_notifications()
    {
        var implementations = Module.GetTypes()
            .Where(type => typeof(INotificationDispatcher).IsAssignableFrom(type))
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .ToList();

        implementations.Should().ContainSingle(
            "one door into the write path is what makes 'modules publish events and never create "
            + "notifications' structural rather than conventional");

        implementations[0].Should().Be<NotificationDispatcher>();
    }

    /// <summary>
    /// <b>Channels remain provider-independent.</b> No channel, sender or message type may name a
    /// vendor. The check is deliberately crude — a substring match on the well-known names — because
    /// the failure it guards against is somebody adding <c>FirebasePushSender</c> to this assembly
    /// rather than to an adapter package.
    /// </summary>
    [Fact]
    public void No_channel_names_a_vendor()
    {
        string[] vendors =
        [
            "Firebase", "Fcm", "Apns", "SendGrid", "Twilio", "Mailgun", "Postmark", "Ses",
            "OneSignal", "WhatsApp", "Sendbird", "Pusher"
        ];

        var offenders = Module.GetTypes()
            .Where(type => vendors.Any(vendor =>
                type.Name.Contains(vendor, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        offenders.Should().BeEmpty(
            "a vendor arrives as an adapter registered against IPushNotificationSender or "
            + "IEmailSender, never as a type in this module. Offenders: "
            + string.Join(", ", offenders.Select(type => type.FullName)));
    }

    /// <summary>
    /// <b>Templates remain transport-independent.</b> The template aggregate must not know what a
    /// push payload limit is, what a MIME part is or what an SMS segment costs. It carries optional
    /// per-channel variants and the channel decides what to do with what it is given.
    /// </summary>
    [Fact]
    public void The_template_aggregate_does_not_depend_on_any_channel()
    {
        var result = Types.InAssembly(Module)
            .That()
            .ResideInNamespace("Diaspora.Modules.Notifications.Domain.Templates")
            .Should()
            .NotHaveDependencyOnAny(
                "Diaspora.Modules.Notifications.Infrastructure.Channels",
                "Diaspora.Modules.Notifications.Extensibility.Channels")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "a template is words, and words do not know how they travel. Offenders: "
            + Describe(result));
    }

    /// <summary>
    /// <b>Routing is policy-driven rather than hard-coded, and this is the test that says so.</b> A
    /// consumer's job is to read a payload and produce triggers; it must never name a template key, a
    /// category key or a priority. Those come from the rule catalogue, which is reference data an
    /// operator edits. The check is a dependency one: nothing under Consumers may reference the
    /// catalogue constants or the priority enum.
    /// </summary>
    [Fact]
    public void No_consumer_names_a_template_a_category_or_a_priority()
    {
        var result = Types.InAssembly(Module)
            .That()
            .ResideInNamespaceStartingWith("Diaspora.Modules.Notifications.Application.Consumers")
            .Should()
            .NotHaveDependencyOnAny(
                "Diaspora.Modules.Notifications.Catalog.Rules.NotificationTemplateKeys",
                "Diaspora.Modules.Notifications.Catalog.NotificationCategoryKeys",
                "Diaspora.Modules.Notifications.Domain.Enums.NotificationPriority",
                "Diaspora.Modules.Notifications.Domain.Enums.DeliveryPolicy")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "a consumer that named a template or a priority would have moved the routing decision "
            + "out of the catalogue and back into code. Offenders: " + Describe(result));
    }

    /// <summary>
    /// <b>No published event declares its own <c>EventId</c>.</b> The base
    /// <see cref="IntegrationEvent"/> already owns that name as the outbox de-duplication key.
    /// Shadowing it compiles with only a CS0108 warning, serialises the wrong value, and silently
    /// breaks every consumer's idempotency — which is why this module's own identifier is
    /// <c>NotificationId</c> throughout.
    /// </summary>
    [Fact]
    public void No_published_event_shadows_the_platform_event_identifier()
    {
        var offenders = typeof(NotificationCreatedIntegrationEvent).Assembly
            .GetTypes()
            .Where(type => typeof(IIntegrationEvent).IsAssignableFrom(type))
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(type => type.GetProperty(
                "EventId",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly) is not null)
            .ToList();

        offenders.Should().BeEmpty(
            "EventId belongs to IntegrationEvent. Offenders: "
            + string.Join(", ", offenders.Select(type => type.FullName)));
    }

    /// <summary>
    /// <b>No published contract carries a notification's words.</b> A message's text names the
    /// person who messaged you, the flat you applied for, the offer you were made — and putting it on
    /// a bus every module reads would make this the platform's largest unnecessary disclosure surface.
    /// </summary>
    [Fact]
    public void No_published_notification_contract_carries_message_text()
    {
        string[] forbidden = ["Title", "Body", "ShortBody", "Content", "Message", "Token"];

        var offenders = typeof(NotificationCreatedIntegrationEvent).Assembly
            .GetTypes()
            .Where(type => type.Namespace?.StartsWith(
                "Diaspora.Contracts.Notifications", StringComparison.Ordinal) == true)
            .SelectMany(type => type.GetProperties()
                .Where(property => forbidden.Contains(property.Name, StringComparer.Ordinal))
                .Select(property => $"{type.Name}.{property.Name}"))
            .ToList();

        offenders.Should().BeEmpty(
            "consumers get identifiers, categories and timings — never the words. Offenders: "
            + string.Join(", ", offenders));
    }

    /// <summary>The module's URL space is declared once and lives under one prefix.</summary>
    [Fact]
    public void The_module_has_one_route_prefix()
    {
        var offenders = SourceTree.ProductionFiles
            .Where(file => file.Path.Contains(
                "Diaspora.Modules.Notifications", StringComparison.Ordinal))
            .Where(file => !file.Path.EndsWith("NotificationsModuleMetadata.cs", StringComparison.Ordinal))
            .Where(file => file.Root.ToFullString().Contains("\"/api/v1/", StringComparison.Ordinal))
            .Select(file => file.Path)
            .ToList();

        offenders.Should().BeEmpty(
            "the route prefix is declared once, in NotificationsModuleMetadata. Offenders: "
            + string.Join(", ", offenders));
    }

    /// <summary>Every contributor the kernel constructs before the container exists is default-constructible.</summary>
    [Fact]
    public void Every_catalogue_contributor_has_a_parameterless_constructor()
    {
        var contributorTypes = new[]
        {
            typeof(IFeatureContributor), typeof(IPermissionContributor), typeof(IPolicyContributor)
        };

        var offenders = Module.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(type => contributorTypes.Any(contract => contract.IsAssignableFrom(type)))
            .Where(type => type.GetConstructor(Type.EmptyTypes) is null)
            .ToList();

        offenders.Should().BeEmpty(
            "the kernel builds these before the container exists. Offenders: "
            + string.Join(", ", offenders.Select(type => type.FullName)));
    }

    private static bool IsCompilerGenerated(Type type) =>
        type.IsNested
        || type.Name.StartsWith('<')
        || type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false);

    private static bool IsIntegrationEventConsumer(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType
                && current.GetGenericTypeDefinition() == typeof(IntegrationEventHandler<>))
            {
                return true;
            }
        }

        return false;
    }

    private static string Describe(TestResult result) =>
        result.FailingTypeNames is null
            ? "none"
            : string.Join(", ", result.FailingTypeNames);
}
