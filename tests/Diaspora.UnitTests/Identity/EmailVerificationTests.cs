using System.Buffers.Text;
using System.Text;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Commands.EmailVerification;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Users;
using Diaspora.Domain.Identity.ValueObjects;
using Diaspora.Infrastructure.Identity.EmailVerification;
using Diaspora.Infrastructure.Identity.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.ValueObjects;
using Diaspora.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Identity;

/// <summary>
/// Email verification — 5 Oct 2026. The signed link, the rule that a proof belongs to one address,
/// and the send rules. The page and the route are exercised end to end in the integration suite.
/// </summary>
public sealed class EmailVerificationTests
{
    private const string Key = "a-signing-key-long-enough-to-pass-the-jwt-validator-0123456789";

    private static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    // ── The link ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_link_names_the_member_and_the_address_it_was_sent_to()
    {
        var links = Links(new FakeClock(Now));
        var userId = UserId.New();

        var (token, expiresAt) = links.Create(userId, "Amara@Example.com");

        expiresAt.Should().Be(Now.AddHours(12));
        links.Redeem(token).Should().Be(new EmailVerificationClaim(userId, "amara@example.com"));
    }

    [Fact]
    public void A_link_dies_after_twelve_hours()
    {
        var clock = new FakeClock(Now);
        var links = Links(clock);
        var (token, _) = links.Create(UserId.New(), "amara@example.com");

        clock.Advance(TimeSpan.FromHours(12));

        links.Redeem(token).Should().BeNull();
    }

    [Fact]
    public void A_link_whose_address_was_edited_is_refused()
    {
        var links = Links(new FakeClock(Now));
        var (token, _) = links.Create(UserId.New(), "amara@example.com");

        var decoded = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(token));
        var forged = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(
            decoded.Replace("amara@example.com", "attacker@example.com", StringComparison.Ordinal)));

        links.Redeem(forged).Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-base64-at-all!!")]
    [InlineData("YWJj")]
    public void Malformed_links_are_refused_without_throwing(string? token) =>
        Links(new FakeClock(Now)).Redeem(token).Should().BeNull();

    [Fact]
    public void A_link_signed_under_another_key_is_refused()
    {
        var (token, _) = Links(new FakeClock(Now)).Create(UserId.New(), "amara@example.com");

        var other = new EmailVerificationLinks(
            Options.Create(new JwtOptions { SigningKey = Key + "-rotated" }), new FakeClock(Now));

        other.Redeem(token).Should().BeNull();
    }

    // ── The proof belongs to one address ─────────────────────────────────────────────────────

    [Fact]
    public void Confirming_marks_the_address_verified_and_a_second_confirmation_changes_nothing()
    {
        var user = Member("amara@example.com");

        user.ConfirmEmailVerified(EmailAddress.Create("amara@example.com"), Now).IsSuccess.Should().BeTrue();
        user.IsEmailVerified.Should().BeTrue();

        user.ConfirmEmailVerified(EmailAddress.Create("amara@example.com"), Now.AddHours(1)).IsSuccess.Should().BeTrue();
        user.Security.EmailVerifiedAt.Should().Be(Now);
    }

    [Fact]
    public void A_link_for_an_address_that_has_since_changed_does_not_verify_the_new_one()
    {
        var user = Member("amara@example.com");
        user.SetEmail(EmailAddress.Create("amara.new@example.com"), Now);

        var result = user.ConfirmEmailVerified(EmailAddress.Create("amara@example.com"), Now);

        result.IsFailure.Should().BeTrue();
        user.IsEmailVerified.Should().BeFalse();
    }

    [Fact]
    public void Changing_a_verified_address_forgets_the_verification()
    {
        var user = Member("amara@example.com");
        user.ConfirmEmailVerified(EmailAddress.Create("amara@example.com"), Now);

        user.SetEmail(EmailAddress.Create("amara.new@example.com"), Now.AddDays(1));

        user.IsEmailVerified.Should().BeFalse();
        user.Security.EmailVerifiedAt.Should().BeNull();
    }

    // ── Sending ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_deployment_with_no_email_transport_spends_no_allowance()
    {
        var limiter = Substitute.For<ISubjectRateLimiter>();
        var handler = SendHandler(Member("amara@example.com"), configured: false, limiter: limiter);

        var result = await handler.Handle(new SendEmailVerificationCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.Email.NotConfigured");
        await limiter.DidNotReceiveWithAnyArgs().AcquireAsync(default!, default, default, default);
    }

    [Fact]
    public async Task A_send_inside_the_cooldown_is_refused_and_says_how_long_to_wait()
    {
        var limiter = Substitute.For<ISubjectRateLimiter>();
        limiter.AcquireAsync(Arg.Is<string>(key => key.Contains("cooldown")), 1, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(RateLimitLease.Denied(TimeSpan.FromSeconds(42)));
        var sender = Substitute.For<IEmailVerificationSender>();
        sender.IsConfigured.Returns(true);

        var send = () => SendHandler(Member("amara@example.com"), sender: sender, limiter: limiter)
            .Handle(new SendEmailVerificationCommand(), CancellationToken.None);

        // Thrown, so the middleware sends Retry-After and the app can count down.
        var refusal = await send.Should().ThrowAsync<Diaspora.Application.Exceptions.RateLimitException>();
        refusal.Which.RetryAfterSeconds.Should().Be(42);
        refusal.Which.Message.Should().Contain("42 seconds");
        await sender.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
    }

    [Fact]
    public async Task A_send_carries_a_token_for_the_address_on_file_and_reports_the_cooldown()
    {
        var limiter = Substitute.For<ISubjectRateLimiter>();
        limiter.AcquireAsync(default!, default, default, default).ReturnsForAnyArgs(RateLimitLease.Allowed(5));
        var sender = Substitute.For<IEmailVerificationSender>();
        sender.IsConfigured.Returns(true);
        sender.SendAsync(default!, default).ReturnsForAnyArgs(true);
        var member = Member("amara@example.com");

        var result = await SendHandler(member, sender: sender, limiter: limiter)
            .Handle(new SendEmailVerificationCommand(), CancellationToken.None);

        result.Value.CooldownSeconds.Should().Be(60);
        result.Value.VerificationExpiresAt.Should().Be(Now.AddHours(12));
        await sender.Received(1).SendAsync(
            Arg.Is<EmailVerificationMessage>(message =>
                message.Address == "amara@example.com"
                && Links(new FakeClock(Now)).Redeem(message.Token)!.UserId == member.Id),
            Arg.Any<CancellationToken>());
    }

    // ── Fixtures ─────────────────────────────────────────────────────────────────────────────

    private static EmailVerificationLinks Links(IClock clock) =>
        new(Options.Create(new JwtOptions { SigningKey = Key }), clock);

    private static User Member(string email)
    {
        var user = User.Register(
            FullName.Create("Amara Okafor"),
            PhoneNumber.Create("+447700900123"),
            PasswordHash.Create(new string('a', 60)),
            CountryCode.Create("NG"),
            CountryCode.Create("GB"),
            LanguageCode.Create("en"),
            TimeZoneIdentifier.Create("Europe/London"),
            Now);

        user.SetEmail(EmailAddress.Create(email), Now);
        return user;
    }

    private static SendEmailVerificationCommandHandler SendHandler(
        User member,
        bool configured = true,
        IEmailVerificationSender? sender = null,
        ISubjectRateLimiter? limiter = null)
    {
        var users = Substitute.For<IUserRepository>();
        users.GetByIdAsync(member.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<User?>(member));

        var current = Substitute.For<ICurrentIdentityContext>();
        current.UserId.Returns(member.Id);

        if (sender is null)
        {
            sender = Substitute.For<IEmailVerificationSender>();
            sender.IsConfigured.Returns(configured);
        }

        return new SendEmailVerificationCommandHandler(
            users, current, Links(new FakeClock(Now)), sender,
            limiter ?? Substitute.For<ISubjectRateLimiter>(), new FakeClock(Now));
    }
}
