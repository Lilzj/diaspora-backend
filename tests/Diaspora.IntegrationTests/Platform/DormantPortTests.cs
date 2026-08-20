using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// The kernel's unimplemented notification port, checked against a booted container.
/// <para>
/// <b>B1.4 recorded <c>INotificationPublisher</c> as F5: declared, never implemented, never called —
/// a port that reads as available and is not.</b> B1.4.1 answered by obsoleting it as a compile
/// error rather than implementing or deleting it, because the architectural rule B13 established is
/// that business modules publish integration events and only the Communication Platform creates
/// notifications. An implementation would be a kernel-level way around that for every module at
/// once.
/// </para>
/// <para>
/// <b>The compile error is the guard; this is the check that the guard is still the whole story.</b>
/// An obsolete attribute stops source that names the type. It does not stop a registration made by
/// reflection, by a container convention that binds every interface in a namespace, or by a package
/// added later — so the question worth asking a real host is the simple one: does anything answer.
/// </para>
/// <para>
/// Reached by metadata name rather than by <c>typeof</c>, because naming the type in source is the
/// error this asserts. A test that suppressed the rule it is testing would be testing the
/// suppression.
/// </para>
/// </summary>
public sealed class DormantPortTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DormantPortTests(CustomWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    [Fact]
    public void Nothing_in_the_composed_host_answers_for_the_notification_publisher()
    {
        var port = Type.GetType(
            "Diaspora.Platform.Services.Abstractions.INotificationPublisher, Diaspora.Platform");

        port.Should().NotBeNull("the port is kept, obsoleted, so the shortcut is answered where it is taken");

        using var scope = _factory.Services.CreateScope();

        scope.ServiceProvider.GetService(port!).Should().BeNull(
            "a resolvable INotificationPublisher is a notification path open to every module, " +
            "which is the one thing the Communication Platform exists to prevent. Publish an " +
            "integration event instead.");
    }

    /// <summary>
    /// The counterpart. The rule above is satisfied by a platform where notifications do not work
    /// at all, so the seam that <em>is</em> supposed to be resolvable has to be resolvable.
    /// </summary>
    [Fact]
    public void The_publisher_a_module_is_supposed_to_use_resolves()
    {
        var publisher = Type.GetType(
            "Diaspora.Platform.Messaging.IIntegrationEventPublisher, Diaspora.Platform");

        publisher.Should().NotBeNull();

        using var scope = _factory.Services.CreateScope();

        scope.ServiceProvider.GetService(publisher!).Should().NotBeNull(
            "this is what a module calls when it wants somebody notified, and B13 decides the rest");
    }
}
