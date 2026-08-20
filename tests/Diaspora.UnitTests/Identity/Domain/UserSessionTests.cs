using Diaspora.Domain.Identity.Devices;
using Diaspora.Domain.Identity.Events;
using Diaspora.Domain.Identity.Sessions;
using Diaspora.SharedKernel.Identity;
using Diaspora.UnitTests.Identity.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Identity.Domain;

/// <summary>
/// Refresh-token rotation and replay detection. The highest-value tests in the module:
/// a defect here hands an attacker a live session.
/// </summary>
public class UserSessionTests
{
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(30);
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(90);

    private static UserSession Start(DateTimeOffset? now = null) =>
        UserSession.Start(
            UserId.New(),
            deviceId: null,
            deviceName: "Pixel 8",
            platform: DevicePlatform.Android,
            appVersion: "1.0.0",
            ipAddress: "203.0.113.10",
            userAgent: "Diaspora/1.0",
            refreshTokenHash: IdentityTestData.Hash("token-1"),
            now: now ?? IdentityTestData.Now,
            refreshTokenLifetime: RefreshLifetime,
            absoluteSessionLifetime: SessionLifetime);

    [Fact]
    public void Starting_a_session_issues_its_first_refresh_token()
    {
        var session = Start();

        session.Status.Should().Be(SessionStatus.Active);
        session.RefreshTokens.Should().ContainSingle();
        session.CurrentRefreshToken(IdentityTestData.Now).Should().NotBeNull();
    }

    [Fact]
    public void Starting_a_session_raises_creation_and_token_events()
    {
        var session = Start();

        session.DomainEvents.Should().Contain(e => e is UserSessionCreatedDomainEvent);
        session.DomainEvents.Should().Contain(e => e is RefreshTokenIssuedDomainEvent);
    }

    [Fact]
    public void Rotation_issues_a_new_token_and_retires_the_old_one()
    {
        var session = Start();
        var original = session.RefreshTokens[0];

        var result = session.RotateRefreshToken(
            IdentityTestData.Hash("token-1"),
            IdentityTestData.Hash("token-2"),
            IdentityTestData.Now.AddMinutes(10),
            RefreshLifetime);

        result.IsSuccess.Should().BeTrue();
        session.RefreshTokens.Should().HaveCount(2);
        original.StatusAt(IdentityTestData.Now.AddMinutes(10)).Should().Be(RefreshTokenStatus.Rotated);
        original.ReplacedByTokenId.Should().Be(result.Value.Id);
    }

    [Fact]
    public void Only_the_newest_token_is_redeemable_after_rotation()
    {
        var session = Start();
        var later = IdentityTestData.Now.AddMinutes(10);

        session.RotateRefreshToken(
            IdentityTestData.Hash("token-1"), IdentityTestData.Hash("token-2"), later, RefreshLifetime);

        session.CurrentRefreshToken(later)!.TokenHash
            .Should().Be(IdentityTestData.Hash("token-2"));
    }

    [Fact]
    public void An_unknown_token_is_rejected_without_touching_the_session()
    {
        var session = Start();

        var result = session.RotateRefreshToken(
            IdentityTestData.Hash("never-issued"),
            IdentityTestData.Hash("token-2"),
            IdentityTestData.Now,
            RefreshLifetime);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.Session.RefreshTokenNotFound");
        session.Status.Should().Be(SessionStatus.Active);
    }

    [Fact]
    public void Replaying_a_rotated_token_is_treated_as_theft_and_kills_the_session()
    {
        var session = Start();
        var later = IdentityTestData.Now.AddMinutes(10);

        session.RotateRefreshToken(
            IdentityTestData.Hash("token-1"), IdentityTestData.Hash("token-2"), later, RefreshLifetime);
        session.ClearDomainEvents();

        // The attacker presents the token the legitimate client already spent.
        var replay = session.RotateRefreshToken(
            IdentityTestData.Hash("token-1"),
            IdentityTestData.Hash("token-3"),
            later.AddMinutes(1),
            RefreshLifetime);

        replay.IsFailure.Should().BeTrue();
        replay.Error.Code.Should().Be("Identity.Session.RefreshTokenReuseDetected");
        session.Status.Should().Be(SessionStatus.Revoked);
        session.RevocationReason.Should().Be(SessionRevocationReason.TokenReuseDetected);
    }

    [Fact]
    public void Replay_detection_revokes_every_token_in_the_chain()
    {
        var session = Start();
        var later = IdentityTestData.Now.AddMinutes(10);

        session.RotateRefreshToken(
            IdentityTestData.Hash("token-1"), IdentityTestData.Hash("token-2"), later, RefreshLifetime);
        session.RotateRefreshToken(
            IdentityTestData.Hash("token-2"), IdentityTestData.Hash("token-3"), later, RefreshLifetime);

        session.RotateRefreshToken(
            IdentityTestData.Hash("token-1"),
            IdentityTestData.Hash("token-4"),
            later.AddMinutes(1),
            RefreshLifetime);

        // Including the one the genuine client is holding — that is the intended trade.
        session.RefreshTokens.Should().OnlyContain(token => token.RevokedAt != null);
        session.CurrentRefreshToken(later.AddMinutes(2)).Should().BeNull();
    }

    [Fact]
    public void Replay_detection_raises_the_reuse_event()
    {
        var session = Start();
        var later = IdentityTestData.Now.AddMinutes(10);

        session.RotateRefreshToken(
            IdentityTestData.Hash("token-1"), IdentityTestData.Hash("token-2"), later, RefreshLifetime);
        session.ClearDomainEvents();

        session.RotateRefreshToken(
            IdentityTestData.Hash("token-1"), IdentityTestData.Hash("token-3"), later, RefreshLifetime);

        session.DomainEvents.Should().Contain(e => e is RefreshTokenReuseDetectedDomainEvent);
        session.DomainEvents.Should().Contain(e => e is UserSessionRevokedDomainEvent);
    }

    [Fact]
    public void Replaying_a_token_from_an_already_revoked_session_is_still_detected()
    {
        var session = Start();
        session.Revoke(SessionRevocationReason.SignedOut, IdentityTestData.Now.AddMinutes(5));
        session.ClearDomainEvents();

        var result = session.RotateRefreshToken(
            IdentityTestData.Hash("token-1"),
            IdentityTestData.Hash("token-2"),
            IdentityTestData.Now.AddMinutes(10),
            RefreshLifetime);

        result.Error.Code.Should().Be("Identity.Session.RefreshTokenReuseDetected");
    }

    [Fact]
    public void An_expired_refresh_token_cannot_be_rotated()
    {
        var session = Start();
        var afterExpiry = IdentityTestData.Now.Add(RefreshLifetime).AddMinutes(1);

        var result = session.RotateRefreshToken(
            IdentityTestData.Hash("token-1"),
            IdentityTestData.Hash("token-2"),
            afterExpiry,
            RefreshLifetime);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.Session.RefreshTokenExpired");
    }

    [Fact]
    public void Rotation_never_extends_a_token_beyond_the_absolute_session_ceiling()
    {
        // The guarantee that keeps a stolen device from holding a session open forever.
        // A short session ceiling against a long refresh lifetime makes the cap the
        // binding constraint from the first rotation.
        var shortCeiling = TimeSpan.FromDays(10);
        var session = UserSession.Start(
            UserId.New(),
            deviceId: null,
            deviceName: "Pixel 8",
            platform: DevicePlatform.Android,
            appVersion: "1.0.0",
            ipAddress: null,
            userAgent: null,
            refreshTokenHash: IdentityTestData.Hash("token-1"),
            now: IdentityTestData.Now,
            refreshTokenLifetime: RefreshLifetime,
            absoluteSessionLifetime: shortCeiling);

        var result = session.RotateRefreshToken(
            IdentityTestData.Hash("token-1"),
            IdentityTestData.Hash("token-2"),
            IdentityTestData.Now.AddDays(5),
            RefreshLifetime);

        result.IsSuccess.Should().BeTrue();
        result.Value.ExpiresAt.Should().Be(session.AbsoluteExpiresAt,
            "a 30-day refresh lifetime must not push the token past a 10-day session ceiling");
    }

    [Fact]
    public void The_first_refresh_token_is_also_capped_by_the_session_ceiling()
    {
        var shortCeiling = TimeSpan.FromDays(3);
        var session = UserSession.Start(
            UserId.New(),
            deviceId: null,
            deviceName: "Pixel 8",
            platform: DevicePlatform.Android,
            appVersion: null,
            ipAddress: null,
            userAgent: null,
            refreshTokenHash: IdentityTestData.Hash("token-1"),
            now: IdentityTestData.Now,
            refreshTokenLifetime: RefreshLifetime,
            absoluteSessionLifetime: shortCeiling);

        session.RefreshTokens[0].ExpiresAt.Should().Be(session.AbsoluteExpiresAt);
    }

    [Fact]
    public void A_session_past_its_absolute_ceiling_expires_rather_than_rotating()
    {
        var session = Start();
        var pastCeiling = IdentityTestData.Now.Add(SessionLifetime).AddDays(1);

        var result = session.RotateRefreshToken(
            IdentityTestData.Hash("token-1"),
            IdentityTestData.Hash("token-2"),
            pastCeiling,
            RefreshLifetime);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.Session.Expired");
        session.Status.Should().Be(SessionStatus.Expired);
    }

    [Fact]
    public void Signing_out_revokes_the_session_and_its_tokens()
    {
        var session = Start();

        var result = session.Revoke(SessionRevocationReason.SignedOut, IdentityTestData.Now.AddHours(1));

        result.IsSuccess.Should().BeTrue();
        session.Status.Should().Be(SessionStatus.Revoked);
        session.RefreshTokens.Should().OnlyContain(token => token.RevokedAt != null);
        session.DomainEvents.Should().Contain(e => e is UserLoggedOutDomainEvent);
    }

    [Fact]
    public void Revoking_twice_is_refused()
    {
        var session = Start();
        session.Revoke(SessionRevocationReason.SignedOut, IdentityTestData.Now);

        var second = session.Revoke(SessionRevocationReason.RevokedByUser, IdentityTestData.Now);

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("Identity.Session.AlreadyRevoked");
    }

    [Fact]
    public void Touching_a_session_moves_its_last_seen_timestamp()
    {
        var session = Start();
        var later = IdentityTestData.Now.AddHours(3);

        session.Touch(later);

        session.LastSeenAt.Should().Be(later);
    }

    [Fact]
    public void Touching_a_session_past_its_ceiling_expires_it()
    {
        var session = Start();

        session.Touch(IdentityTestData.Now.Add(SessionLifetime).AddDays(1));

        session.Status.Should().Be(SessionStatus.Expired);
    }

    [Fact]
    public void A_revoked_session_reports_as_inactive()
    {
        var session = Start();
        session.Revoke(SessionRevocationReason.AdministrativeAction, IdentityTestData.Now);

        session.IsActiveAt(IdentityTestData.Now).Should().BeFalse();
    }
}
