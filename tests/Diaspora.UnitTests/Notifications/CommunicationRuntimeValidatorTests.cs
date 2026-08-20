using System.Reflection;
using Diaspora.Modules.Notifications.Extensibility.Channels;
using Diaspora.Modules.Notifications.Infrastructure.Channels;
using Diaspora.Modules.Notifications.Options;
using Diaspora.Modules.Notifications.Validation;
using Diaspora.Platform.Validation;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Notifications;

/// <summary>
/// The boot-time refusals.
/// <para>
/// <b>Every finding here guards a failure that produces no exception, no log line and no failing
/// test.</b> A deployment scaled to three instances with no backplane serves two thirds of its
/// members nothing and reports itself perfectly healthy. A push provider named with no adapter
/// behind it records every delivery unsupported. The loopback sender in production marks every push
/// delivered while no phone ever lights up. All three are configuration, all three are silent, and
/// the only place they can be caught is before the host starts.
/// </para>
/// </summary>
public class CommunicationRuntimeValidatorTests
{
    private readonly CommunicationRuntimeValidator _validator = new();

    [Fact]
    public void The_shipped_defaults_produce_nothing_to_report()
    {
        Validate(new RealtimeOptions(), new PushProviderOptions())
            .Should().BeEmpty(
                "a fresh clone must boot clean, or the report becomes noise nobody reads");
    }

    /// <summary>
    /// The finding this validator exists for. Nothing at runtime can count its own replicas, so a
    /// platform that guessed would guess one and be wrong on every scaled deployment.
    /// </summary>
    [Fact]
    public void More_than_one_instance_without_a_backplane_refuses_to_start()
    {
        var realtime = new RealtimeOptions { InstanceCount = 3 };

        var finding = Validate(realtime, new PushProviderOptions())
            .Should().ContainSingle(f => f.Code == "SV7002").Which;

        finding.Severity.Should().Be(StartupFindingSeverity.Error);
        finding.Message.Should().Contain("backplane");
    }

    [Fact]
    public void More_than_one_instance_with_a_declared_backplane_is_accepted()
    {
        var realtime = new RealtimeOptions
        {
            InstanceCount = 3,
            Backplane = new RealtimeBackplaneOptions { Provider = "Redis" }
        };

        Validate(realtime, new PushProviderOptions()).Should().BeEmpty();
    }

    /// <summary>
    /// Below twice the keep-alive, healthy clients are disconnected between pings and reconnect
    /// forever — which reads as flapping presence, not as a timeout that is too short.
    /// </summary>
    [Fact]
    public void A_client_timeout_below_twice_the_keep_alive_is_reported()
    {
        var realtime = new RealtimeOptions { KeepAliveSeconds = 15, ClientTimeoutSeconds = 20 };

        Validate(realtime, new PushProviderOptions())
            .Should().ContainSingle(f => f.Code == "SV7004")
            .Which.Severity.Should().Be(StartupFindingSeverity.Warning);
    }

    [Fact]
    public void A_disabled_runtime_is_a_warning_and_suppresses_the_rest()
    {
        var realtime = new RealtimeOptions { Enabled = false, InstanceCount = 5 };

        var findings = Validate(realtime, new PushProviderOptions());

        findings.Should().ContainSingle().Which.Code.Should().Be("SV7001");
        findings.Should().NotContain(f => f.Code == "SV7002",
            "reporting on a subsystem that is not running is noise on a boot log");
    }

    /// <summary>
    /// "No push provider" is the platform's shipped, documented position. Warning about it every
    /// boot would train operators to ignore this report.
    /// </summary>
    [Fact]
    public void No_push_provider_is_not_a_finding()
    {
        Validate(new RealtimeOptions(), new PushProviderOptions { Provider = "None" })
            .Should().BeEmpty();
    }

    [Fact]
    public void A_named_provider_with_no_adapter_refuses_to_start()
    {
        var push = new PushProviderOptions
        {
            Provider = "SomeVendor",
            ProjectId = "project",
            CredentialsPath = "/run/secrets/push.json"
        };

        Validate(new RealtimeOptions(), push)
            .Should().ContainSingle(f => f.Code == "SV7008")
            .Which.Severity.Should().Be(StartupFindingSeverity.Error);
    }

    [Fact]
    public void A_named_provider_with_an_adapter_registered_is_accepted()
    {
        var push = new PushProviderOptions
        {
            Provider = "SomeVendor",
            ProjectId = "project",
            CredentialsPath = "/run/secrets/push.json"
        };

        Validate(new RealtimeOptions(), push, withPushAdapter: true).Should().BeEmpty();
    }

    [Fact]
    public void A_named_provider_with_no_credential_refuses_to_start()
    {
        var push = new PushProviderOptions { Provider = "SomeVendor", ProjectId = "project" };

        Validate(new RealtimeOptions(), push, withPushAdapter: true)
            .Should().ContainSingle(f => f.Code == "SV7009")
            .Which.Severity.Should().Be(StartupFindingSeverity.Error);
    }

    /// <summary>
    /// Two sources for one credential is a deployment that cannot say which one it authenticated
    /// with — which is how a rotation gets applied to the file while the process keeps using an
    /// environment variable set last year.
    /// </summary>
    [Fact]
    public void Two_credential_sources_refuse_to_start()
    {
        var push = new PushProviderOptions
        {
            Provider = "SomeVendor",
            ProjectId = "project",
            CredentialsPath = "/run/secrets/push.json",
            CredentialsJson = "{}"
        };

        Validate(new RealtimeOptions(), push, withPushAdapter: true)
            .Should().ContainSingle(f => f.Code == "SV7010")
            .Which.Severity.Should().Be(StartupFindingSeverity.Error);
    }

    [Fact]
    public void The_loopback_sender_is_permitted_in_development()
    {
        var push = new PushProviderOptions { Provider = PushProviderOptions.LoopbackProvider };

        Validate(new RealtimeOptions(), push, environmentName: "Development").Should().BeEmpty();
    }

    /// <summary>
    /// A deployment that marks every push delivered while no device is reached is confidently
    /// wrong, which is strictly worse than one honestly reporting that nothing is configured.
    /// </summary>
    [Fact]
    public void The_loopback_sender_refuses_to_start_outside_development()
    {
        var push = new PushProviderOptions { Provider = PushProviderOptions.LoopbackProvider };

        Validate(new RealtimeOptions(), push, environmentName: "Production")
            .Should().ContainSingle(f => f.Code == "SV7007")
            .Which.Severity.Should().Be(StartupFindingSeverity.Error);
    }

    [Fact]
    public void An_inline_credential_outside_development_is_worth_saying_out_loud()
    {
        var push = new PushProviderOptions
        {
            Provider = "SomeVendor",
            ProjectId = "project",
            CredentialsJson = "{}"
        };

        Validate(new RealtimeOptions(), push, withPushAdapter: true, environmentName: "Production")
            .Should().ContainSingle(f => f.Code == "SV7011")
            .Which.Severity.Should().Be(StartupFindingSeverity.Warning);
    }

    private List<StartupFinding> Validate(
        RealtimeOptions realtime,
        PushProviderOptions push,
        bool withPushAdapter = false,
        string environmentName = "Development")
    {
        var services = new ServiceCollection();

        services.AddSingleton<IOptions<RealtimeOptions>>(Options.Create(realtime));
        services.AddSingleton<IOptions<PushProviderOptions>>(Options.Create(push));
        services.AddSingleton<IHostEnvironment>(new StubEnvironment(environmentName));

        // The shipped placeholder is always registered by the module, so the adapter check has to
        // distinguish "something else as well" from "only the placeholder" rather than counting.
        services.AddScoped<IPushNotificationSender, UnconfiguredPushNotificationSender>();

        if (withPushAdapter)
        {
            services.AddScoped<IPushNotificationSender, StubPushAdapter>();
        }

        var registrations = services.ToList();
        var provider = services.BuildServiceProvider();

        return _validator
            .Validate(new StartupValidationContext(
                provider, registrations, [Assembly.GetExecutingAssembly()]))
            .ToList();
    }

    private sealed class StubPushAdapter : IPushNotificationSender
    {
        public bool IsConfigured => true;

        public Task<PushSendResult> SendAsync(
            PushMessage message, CancellationToken cancellationToken = default) =>
            Task.FromResult(PushSendResult.Success());
    }

    private sealed class StubEnvironment : IHostEnvironment
    {
        public StubEnvironment(string environmentName) => EnvironmentName = environmentName;

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "Diaspora.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
