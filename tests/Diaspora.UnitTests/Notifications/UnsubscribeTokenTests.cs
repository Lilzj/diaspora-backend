using Diaspora.Modules.Notifications.Application.Preferences;
using Diaspora.Modules.Notifications.Domain.ValueObjects;
using Diaspora.Modules.Notifications.Options;
using Diaspora.SharedKernel.Identity;
using Diaspora.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Notifications;

/// <summary>
/// The one credential on this platform that arrives with no session behind it — B13.3.
/// <para>
/// An unsubscribe link has to work for a member who has stopped opening the app, which is precisely
/// the member it exists for. That makes it unauthenticated, and everything below is about keeping the
/// authority it carries as small as the job needs: one member, one category, an expiry that is part
/// of what was signed, and no way to read anything.
/// </para>
/// </summary>
public class UnsubscribeTokenTests
{
    private static readonly UserId Alice =
        UserId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    private readonly FakeClock _clock = new();

    [Fact]
    public void A_token_round_trips_to_the_member_and_category_it_names()
    {
        var factory = Build();

        var claim = factory.Redeem(factory.Create(Alice, "marketplace"));

        claim.Should().NotBeNull();
        claim!.UserId.Should().Be(Alice);
        claim.CategoryKey.Should().Be("marketplace");
    }

    /// <summary>
    /// The category is lower-cased on the way in, so a footer built from a display label and one
    /// built from a catalogue key redeem to the same category rather than to one that does not exist.
    /// </summary>
    [Fact]
    public void A_category_is_normalised_before_it_is_signed()
    {
        var factory = Build();

        factory.Redeem(factory.Create(Alice, "  MarketPlace "))!.CategoryKey
            .Should().Be("marketplace");
    }

    /// <summary>
    /// <b>The classic version of this bug is signing the subject and not the deadline</b>, which
    /// yields a link that never stops working. The expiry is inside the signed payload, so editing it
    /// invalidates the signature and the token is refused as a forgery rather than honoured as a
    /// fresh one.
    /// </summary>
    [Fact]
    public void A_token_stops_working_once_it_has_expired()
    {
        var factory = Build(lifetimeDays: 1);
        var token = factory.Create(Alice, "marketplace");

        _clock.Advance(TimeSpan.FromDays(2));

        factory.Redeem(token).Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-base64url!!")]
    [InlineData("YWJj")]
    public void A_malformed_token_is_refused_rather_than_throwing(string token)
    {
        Build().Redeem(token).Should().BeNull();
    }

    /// <summary>
    /// A token minted against one deployment's key must not verify against another's. Without this
    /// the signing key is decorative, because anybody who can construct the payload can construct the
    /// token.
    /// </summary>
    [Fact]
    public void A_token_signed_with_a_different_key_is_refused()
    {
        var token = Build(key: "one-signing-key-for-tests-only!!").Create(Alice, "marketplace");

        Build(key: "another-signing-key-for-tests!!").Redeem(token).Should().BeNull();
    }

    /// <summary>
    /// Every field is covered by the signature, so an edited category cannot ride a valid one. This
    /// is the attack the separator choice exists for: two distinct field sets must never canonicalise
    /// to one string.
    /// </summary>
    [Fact]
    public void An_edited_token_is_refused()
    {
        var factory = Build();
        var token = factory.Create(Alice, "marketplace");

        // Flip one character of the payload. Base64url has no padding here, so this stays decodable
        // and reaches the signature check rather than failing as malformed.
        var edited = token[..^1] + (token[^1] == 'A' ? 'B' : 'A');

        factory.Redeem(edited).Should().BeNull();
    }

    // ── The state the token writes ──────────────────────────────────────────────────────────

    /// <summary>
    /// A mail client that prefetches links, a corporate scanner following every URL, and a member who
    /// clicks twice all arrive more than once. The record must say when they asked, not when the
    /// scanner last looked.
    /// </summary>
    [Fact]
    public void Unsubscribing_twice_keeps_the_first_answer()
    {
        var first = CategoryPreference
            .Create("marketplace", email: true)
            .Unsubscribed(CategoryPreference.Sources.EmailLink, _clock.UtcNow);

        var asked = first.UnsubscribedAt;

        _clock.Advance(TimeSpan.FromMinutes(5));

        var second = first.Unsubscribed(CategoryPreference.Sources.App, _clock.UtcNow);

        second.UnsubscribedAt.Should().Be(asked);
        second.UnsubscribeSource.Should().Be(CategoryPreference.Sources.EmailLink);
    }

    /// <summary>
    /// Email on with an unsubscribe stamp is a contradiction, and it is reachable — the update
    /// command rebuilds this object from a request that names channels and knows nothing about
    /// unsubscribe history.
    /// </summary>
    [Fact]
    public void Turning_email_back_on_clears_the_unsubscribe_record()
    {
        var resubscribed = CategoryPreference.Create(
            "marketplace",
            email: true,
            unsubscribedAt: _clock.UtcNow,
            unsubscribeSource: CategoryPreference.Sources.EmailLink);

        resubscribed.UnsubscribedAt.Should().BeNull();
        resubscribed.UnsubscribeSource.Should().BeNull();
    }

    private UnsubscribeTokenFactory Build(
        string key = "a-test-signing-key-of-sufficient-length", int lifetimeDays = 90) =>
        new(
            Options.Create(new NotificationOptions
            {
                UnsubscribeSigningKey = key,
                UnsubscribeLinkLifetimeDays = lifetimeDays
            }),
            _clock,
            NullLogger<UnsubscribeTokenFactory>.Instance);
}
