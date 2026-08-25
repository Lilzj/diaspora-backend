using Diaspora.Application.Identity.Abstractions;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Resilience;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.Sms;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Sms;

/// <summary>
/// The adapter composes into a container that can actually start.
///
/// <para>
/// <b>The defect this file is the memory of.</b> The two per-provider failure classifiers were
/// registered with <c>TryAddEnumerable</c> and a factory. That combination throws:
/// <c>TryAddEnumerable</c> de-duplicates on the descriptor's implementation type, a factory
/// descriptor has none, and it refuses the registration as *"indistinguishable from other services
/// registered for IFailureClassifier"*. The host aborted before binding a port, restarted ten
/// times, and stopped.
/// </para>
///
/// <para>
/// 4,338 unit tests passed over it, including 48 for this adapter. Every one of them constructed
/// the types by hand — which is the right way to test behaviour and is completely blind to how
/// they are wired. Nothing anywhere built the container, so the deployment was the first thing
/// that could possibly notice, and it noticed by falling over.
/// </para>
///
/// <para>
/// So these tests build a real <see cref="ServiceProvider"/>. They assert almost nothing about
/// behaviour and everything about composition, because composition is the part that had no oracle.
/// </para>
/// </summary>
public sealed class SmsRegistrationTests
{
    [Fact]
    public void A_configured_adapter_composes_without_throwing()
    {
        // The assertion the defect failed. `AddSmsAdapter` threw during registration, so this
        // never got as far as resolving anything.
        var act = () => Build(Configured());

        act.Should().NotThrow();
    }

    [Fact]
    public void Both_provider_classifiers_are_registered_and_distinct()
    {
        // Two instances of one type, differing by constructor argument. This is precisely the case
        // `TryAddEnumerable` exists to prevent — had it de-duplicated rather than thrown, one
        // provider would silently have run under the platform's default classifier, which treats
        // an unrecognised failure as permanent and so switches retry off.
        var classifiers = Build(Configured())
            .GetServices<IFailureClassifier>()
            .Where(classifier => classifier is SmsFailureClassifier)
            .Select(classifier => classifier.Dependency)
            .ToList();

        classifiers.Should().BeEquivalentTo([SmsDependencies.Twilio, SmsDependencies.AfricasTalking]);
    }

    [Fact]
    public void The_delivery_service_resolves()
    {
        // The whole point of the adapter: something else's `IOtpDeliveryService` is now ours.
        //
        // Resolved inside a scope, because it is registered scoped — matching the placeholder it
        // replaces, and required by `ILocalizedMessageProvider`, which reads the current request's
        // language. Reaching for it on the root provider is what a background job would do, and
        // `ValidateScopes` refusing that here is the point of turning it on.
        using var scope = Build(Configured()).CreateScope();

        var delivery = scope.ServiceProvider.GetRequiredService<IOtpDeliveryService>();

        delivery.Should().BeOfType<RoutedSmsOtpDeliveryService>();
    }

    [Fact]
    public void Its_resilience_policies_reach_the_catalogue()
    {
        // A dependency name with no policy registered under it falls back to `platform.default`
        // silently — the failure mode `OperationalPolicyNames` warns about, with no error anywhere.
        var catalog = new OperationalPolicyCatalog(
            Build(Configured()).GetServices<IOperationalPolicyContributor>());

        catalog.Find<RetryPolicy>(SmsDependencies.Twilio).Should().NotBeNull();
        catalog.Find<RetryPolicy>(SmsDependencies.AfricasTalking).Should().NotBeNull();
        catalog.Find<CircuitBreakerPolicy>(SmsDependencies.Twilio).Should().NotBeNull();
        catalog.Find<CircuitBreakerPolicy>(SmsDependencies.AfricasTalking).Should().NotBeNull();
    }

    [Fact]
    public void The_router_prefers_the_routed_provider()
    {
        // Composition again rather than routing — `SmsRoutingTests` covers the rules. What is
        // asserted here is that the gateways registered by the container are the ones the router
        // receives, which hand-built tests cannot see.
        var route = Build(Configured()).GetRequiredService<SmsGatewayRouter>().Resolve("+2348100200001");

        route!.Primary.Provider.Should().Be(SmsProviderNames.AfricasTalking);
        route.Fallback!.Provider.Should().Be(SmsProviderNames.Twilio);
    }

    [Fact]
    public void An_unconfigured_deployment_keeps_the_placeholder()
    {
        // The no-op path, which is what every development host takes. Registering here would put
        // a gateway with no credentials in front of the logging placeholder and turn a working
        // local flow into a failing one.
        var services = Bare();
        services.AddSmsAdapter(new ConfigurationBuilder().Build());

        services.Any(descriptor => descriptor.ServiceType == typeof(IOtpDeliveryService))
            .Should().BeFalse();
    }

    [Fact]
    public void A_section_with_only_comments_is_still_unconfigured()
    {
        // `appsettings.json` ships the documented shape with empty values and `//` keys. If those
        // counted as configuration, every deployment would register a credential-less adapter.
        var services = Bare();

        services.AddSmsAdapter(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sms:DefaultProvider"] = "",
                ["Sms:TimeoutSeconds"] = "10",
                ["Sms:Twilio:AccountSid"] = "",
                ["Sms:AfricasTalking:Username"] = "",
            })
            .Build());

        services.Any(descriptor => descriptor.ServiceType == typeof(IOtpDeliveryService))
            .Should().BeFalse();
    }

    // ── Fixtures ────────────────────────────────────────────────────────────

    private static IConfiguration Configured() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sms:DefaultProvider"] = SmsProviderNames.Twilio,
                ["Sms:Twilio:AccountSid"] = "AC1",
                ["Sms:Twilio:AuthToken"] = "token",
                ["Sms:Twilio:From"] = "+441234567890",
                ["Sms:AfricasTalking:Username"] = "sandbox",
                ["Sms:AfricasTalking:ApiKey"] = "key",
                ["Sms:Routes:0:Provider"] = SmsProviderNames.AfricasTalking,
                ["Sms:Routes:0:CallingCodes:0"] = "+234",
            })
            .Build();

    /// <summary>The services the adapter expects the host to have registered already.</summary>
    private static ServiceCollection Bare()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(Substitute.For<IResilienceExecutor>());
        services.AddSingleton(Substitute.For<ILocalizedMessageProvider>());
        services.AddSingleton(Substitute.For<IClock>());

        return services;
    }

    private static ServiceProvider Build(IConfiguration configuration)
    {
        var services = Bare();
        services.AddSmsAdapter(configuration);

        // `validateScopes` and `validateOnBuild` on purpose: the first catches a singleton that
        // captures a scoped service, the second surfaces an unresolvable dependency here rather
        // than on the first request that needs it.
        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}
