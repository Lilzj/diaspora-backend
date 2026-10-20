using System.Buffers.Text;
using System.Text;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Commands.EmailChange;
using Diaspora.Domain.Identity.Events;
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
using Xunit;

namespace Diaspora.UnitTests.Identity;

/// <summary>
/// Changing the sign-in address — 20 Oct 2026. The signed change link, the masking used in notices
/// to the old address, and the aggregate's rule. The routes, the page and every refusal are
/// exercised end to end in the integration suite.
/// </summary>
public sealed class EmailChangeTests
{
    private const string Key = "a-signing-key-long-enough-to-pass-the-jwt-validator-0123456789";

    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    // ── The link ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_link_carries_both_addresses_the_stamp_and_the_session_that_asked()
    {
        var links = Links(new FakeClock(Now));
        var claim = new EmailChangeClaim(UserId.New(), "Old@Example.com", "New@Example.com", 7, SessionId.New());

        var (token, expiresAt) = links.Create(claim);

        expiresAt.Should().Be(Now.AddHours(2));
        links.Redeem(token).Should().Be(claim with { FromAddress = "old@example.com", ToAddress = "new@example.com" });
    }

    [Fact]
    public void A_link_without_a_session_reads_back_without_one()
    {
        var links = Links(new FakeClock(Now));
        var claim = new EmailChangeClaim(UserId.New(), "old@example.com", "new@example.com", 0, null);

        links.Redeem(links.Create(claim).Token).Should().Be(claim);
    }

    [Fact]
    public void A_link_dies_after_two_hours()
    {
        var clock = new FakeClock(Now);
        var links = Links(clock);
        var (token, _) = links.Create(Claim());

        clock.Advance(TimeSpan.FromHours(2));

        links.Redeem(token).Should().BeNull();
    }

    [Theory]
    [InlineData("new@example.com", "attacker@example.com")]
    [InlineData("old@example.com", "victim@example.com")]
    [InlineData("|3|", "|4|")]
    public void A_link_with_any_field_edited_is_refused(string original, string replacement)
    {
        var links = Links(new FakeClock(Now));
        var (token, _) = links.Create(Claim(stamp: 3));

        var decoded = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(token));
        decoded.Should().Contain(original);
        var forged = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(
            decoded.Replace(original, replacement, StringComparison.Ordinal)));

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
    public void A_verification_link_is_not_a_change_link_and_a_change_link_is_not_a_verification_link()
    {
        var options = Options.Create(new JwtOptions { SigningKey = Key });
        var verification = new EmailVerificationLinks(options, new FakeClock(Now));
        var change = new EmailChangeLinks(options, new FakeClock(Now));
        var userId = UserId.New();

        change.Redeem(verification.Create(userId, "new@example.com").Token).Should().BeNull();
        verification.Redeem(change.Create(Claim(userId)).Token).Should().BeNull();
    }

    [Fact]
    public void A_link_signed_under_another_key_is_refused()
    {
        var (token, _) = Links(new FakeClock(Now)).Create(Claim());

        var other = new EmailChangeLinks(
            Options.Create(new JwtOptions { SigningKey = Key + "-rotated" }), new FakeClock(Now));

        other.Redeem(token).Should().BeNull();
    }

    // ── Masking ──────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("joshua@gmail.com", "j*****@g****.com")]
    [InlineData("a@b.io", "*@*.io")]
    [InlineData("amara.okafor.longname@example.co.uk", "a********@e********.uk")]
    [InlineData("nodomain@", "***")]
    public void A_notice_shows_only_the_first_letters_of_the_new_address(string address, string expected) =>
        EmailAddressMask.Mask(address).Should().Be(expected);

    [Fact]
    public void A_mask_never_contains_the_new_address()
    {
        var masked = EmailAddressMask.Mask("attacker.inbox@evil.example");

        masked.Should().NotContain("attacker").And.NotContain("evil");
    }

    // ── The aggregate ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Changing_moves_the_address_marks_it_verified_and_announces_the_verification()
    {
        var user = Member("amara@example.com");
        user.ClearDomainEvents();

        var result = user.ChangeEmail(EmailAddress.Create("amara.new@example.com"), Now.AddHours(1));

        result.IsSuccess.Should().BeTrue();
        user.Email!.Value.Should().Be("amara.new@example.com");
        user.IsEmailVerified.Should().BeTrue();
        user.Security.EmailVerifiedAt.Should().Be(Now.AddHours(1));
        user.DomainEvents.OfType<UserEmailVerifiedDomainEvent>().Should().ContainSingle()
            .Which.EmailAddress.Should().Be("amara.new@example.com");
    }

    [Fact]
    public void Changing_to_the_address_already_on_the_account_is_refused()
    {
        var user = Member("amara@example.com");

        user.ChangeEmail(EmailAddress.Create("amara@example.com"), Now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Changing_the_address_does_not_rotate_the_security_stamp()
    {
        // The session that asked survives; a rotated stamp would sign it out along with the rest.
        var user = Member("amara@example.com");
        var stamp = user.Security.SecurityStamp;

        user.ChangeEmail(EmailAddress.Create("amara.new@example.com"), Now);

        user.Security.SecurityStamp.Should().Be(stamp);
    }

    // ── Fixtures ─────────────────────────────────────────────────────────────────────────────

    private static EmailChangeClaim Claim(UserId? userId = null, int stamp = 0) =>
        new(userId ?? UserId.New(), "old@example.com", "new@example.com", stamp, SessionId.New());

    private static EmailChangeLinks Links(IClock clock) =>
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
}
