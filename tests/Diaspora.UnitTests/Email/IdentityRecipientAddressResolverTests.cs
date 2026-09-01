using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Users;
using Diaspora.Domain.Identity.ValueObjects;
using Diaspora.Email;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Email;

/// <summary>
/// Where a notification email is actually sent.
/// </summary>
/// <remarks>
/// <para>
/// Written after every notification email on the deployment failed with <c>NoEndpoint</c> — the
/// transport configured, the template rendered, the preferences and quiet hours evaluated, the
/// message composed, and nothing that knew the member's address.
/// </para>
/// <para>
/// Notifications had documented that outcome precisely: acquiring an email provider is a sender
/// <em>and</em> an adapter Identity is willing to answer. Only the first was built, and nothing
/// failed loudly, because <c>NoEndpoint</c> is a legitimate answer that the module reports
/// truthfully. The lesson these tests exist to hold is that a channel with no endpoints is not the
/// same as a channel that is switched off.
/// </para>
/// </remarks>
public sealed class IdentityRecipientAddressResolverTests
{
    [Fact]
    public async Task A_members_email_address_becomes_an_endpoint()
    {
        var userId = UserId.New();
        var resolver = Resolver(userId, "member@diaspora.test");

        var endpoints = await resolver.ResolveAsync(userId, NotificationChannelKind.Email);

        endpoints.Should().ContainSingle();
        endpoints[0].Address.Should().Be("member@diaspora.test");
    }

    [Fact]
    public async Task The_endpoint_carries_no_registration_identifier()
    {
        // `EndpointId` means "a registration this module may retire". An address read from Identity
        // is not one, and retiring it would mean Notifications deleting a member's email address
        // because a provider bounced a single message.
        var userId = UserId.New();

        var endpoints = await Resolver(userId, "member@diaspora.test")
            .ResolveAsync(userId, NotificationChannelKind.Email);

        endpoints[0].EndpointId.Should().BeNull();
    }

    [Fact]
    public async Task A_member_with_no_address_resolves_to_nothing()
    {
        // Ordinary rather than exceptional: an account created before email became the sign-in
        // credential may carry none. `NoEndpoint` is the truthful outcome.
        var userId = UserId.New();

        var endpoints = await Resolver(userId, email: null)
            .ResolveAsync(userId, NotificationChannelKind.Email);

        endpoints.Should().BeEmpty();
    }

    [Fact]
    public async Task An_unknown_member_resolves_to_nothing()
    {
        var users = Substitute.For<IUserRepository>();
        users.GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var endpoints = await new IdentityRecipientAddressResolver(users)
            .ResolveAsync(UserId.New(), NotificationChannelKind.Email);

        endpoints.Should().BeEmpty();
    }

    [Theory]
    [InlineData(NotificationChannelKind.Push)]
    [InlineData(NotificationChannelKind.InApp)]
    [InlineData(NotificationChannelKind.Sms)]
    public async Task It_answers_for_no_channel_but_email(NotificationChannelKind channel)
    {
        // A push token is a device registration and belongs to the registry that owns devices.
        // Answering with an email address there would be worse than answering with nothing.
        var userId = UserId.New();

        var endpoints = await Resolver(userId, "member@diaspora.test").ResolveAsync(userId, channel);

        endpoints.Should().BeEmpty();
    }

    [Fact]
    public async Task It_reads_the_address_at_send_time_rather_than_caching_it()
    {
        // Nothing is copied into Notifications, so a member who corrects their address is reached
        // at the corrected one on the very next message.
        var userId = UserId.New();
        var users = Substitute.For<IUserRepository>();
        users.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(UserWith("first@diaspora.test"), UserWith("second@diaspora.test"));

        var resolver = new IdentityRecipientAddressResolver(users);

        (await resolver.ResolveAsync(userId, NotificationChannelKind.Email))[0]
            .Address.Should().Be("first@diaspora.test");
        (await resolver.ResolveAsync(userId, NotificationChannelKind.Email))[0]
            .Address.Should().Be("second@diaspora.test");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static IdentityRecipientAddressResolver Resolver(UserId userId, string? email)
    {
        var users = Substitute.For<IUserRepository>();
        users.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(UserWith(email));

        return new IdentityRecipientAddressResolver(users);
    }

    private static User? UserWith(string? email)
    {
        var user = User.Register(
            FullName.Create("Amara Okafor"),
            PhoneNumber.Create("+2348012345678"),
            PasswordHash.Create(new string('a', 60)),
            CountryCode.Create("NG"),
            CountryCode.Create("MU"),
            LanguageCode.Create("en"),
            TimeZoneIdentifier.Create("Indian/Mauritius"),
            DateTimeOffset.UnixEpoch);

        if (email is not null && EmailAddress.TryCreate(email, out var address) && address is not null)
        {
            user.SetEmail(address, DateTimeOffset.UnixEpoch);
        }

        return user;
    }
}
