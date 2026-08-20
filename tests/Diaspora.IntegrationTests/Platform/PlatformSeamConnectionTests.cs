using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Conversations.Extensibility;
using Diaspora.Modules.Conversations.Extensibility.Policies;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.Platform.Services.Models;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// Seams PSR-1.2 connected, checked against the composed host rather than against a constructor.
/// <para>
/// <b>Every one of these is a unit test that would have passed before the phase.</b> A policy class
/// that nothing registers, an implementation the container never resolves, a port a module forgot to
/// take over — all of them compile, and all of them are what "the seam is unused" looks like from
/// inside the assembly that declares it. What can only be asked of a booted container is whether
/// anything actually answers.
/// </para>
/// </summary>
public sealed class PlatformSeamConnectionTests : IdentityTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public PlatformSeamConnectionTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Kernel ports ─────────────────────────────────────────────────────

    /// <summary>
    /// <b>The kernel's eligibility port had a stub for an implementation and no consumer at all.</b>
    /// <c>DefaultEligibilityService</c> returns <c>Eligible</c> for every action for every member,
    /// while the Trust module holds the platform's real evaluator — the rule chain, the contributed
    /// thresholds, the rate limits — and serves it over HTTP. B3 took over <c>ITrustService</c>
    /// because ADR-002 named it; nothing named this one.
    /// </summary>
    [Fact]
    public void The_eligibility_port_is_answered_by_the_module_that_owns_eligibility()
    {
        using var scope = Factory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<IEligibilityService>()
            .Should().BeOfType<TrustEligibilityService>(
                "a kernel port answered by a placeholder is a question the platform cannot ask");
    }

    [Fact]
    public void The_trust_port_is_answered_by_the_trust_module()
    {
        using var scope = Factory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<ITrustService>()
            .Should().BeOfType<TrustEngineService>();
    }

    /// <summary>
    /// Both ports are Trust's, both are taken over by <c>services.Replace</c>, and neither
    /// placeholder may still be resolvable behind them.
    /// </summary>
    [Fact]
    public void Neither_kernel_placeholder_is_still_registered()
    {
        using var scope = Factory.Services.CreateScope();

        scope.ServiceProvider.GetServices<ITrustService>()
            .Should().ContainSingle().Which.Should().BeOfType<TrustEngineService>();

        scope.ServiceProvider.GetServices<IEligibilityService>()
            .Should().ContainSingle().Which.Should().BeOfType<TrustEligibilityService>();
    }

    // ── Conversation policy ──────────────────────────────────────────────

    /// <summary>
    /// B13.2 shipped <see cref="IConversationPolicy"/> consulted and unimplemented, and recorded the
    /// consequence in one sentence: <em>"Trust's account-level bans do not currently prevent opening
    /// a conversation."</em>
    /// </summary>
    [Fact]
    public void The_conversation_policy_seam_has_production_implementations()
    {
        using var scope = Factory.Services.CreateScope();

        var policies = scope.ServiceProvider.GetServices<IConversationPolicy>().ToList();

        policies.Select(policy => policy.Name).Should().BeEquivalentTo(
        [
            ConversationPolicyNames.InitiatorAccountStanding,
            ConversationPolicyNames.RecipientAccountStanding
        ]);
    }

    /// <summary>
    /// End to end, through the same handler a client reaches: a member the platform has suspended
    /// cannot open a new conversation with anybody.
    /// <para>
    /// The suspension is applied through the aggregate rather than through Trust's staff endpoint,
    /// because the harness cannot mint a staff token — DEBT-031, the platform's largest coverage
    /// gap. What is being asserted is the join, not the moderation workflow, and the join begins at
    /// the stored account health.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_suspended_member_cannot_open_a_conversation_over_http()
    {
        var (_, suspended) = await RegisterAndVerifyAsync();
        var (_, other) = await RegisterAndVerifyAsync();

        await SuspendAsync(UserId.From(suspended.User.Id));

        Api.Authenticate(suspended.Tokens.AccessToken);

        var response = await OpenAsync(other.User.Id, "suspended-initiator");

        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "a suspension that reaches every publishing surface and not the messaging one is a "
            + "suspension somebody can talk their way around");
    }

    /// <summary>
    /// The other side of the pair, and the property that keeps it from leaking: a member cannot open
    /// a conversation with a suspended account, and the refusal is the one a block produces.
    /// </summary>
    [Fact]
    public async Task Nobody_can_open_a_conversation_with_a_suspended_member_and_the_refusal_says_why_not()
    {
        var (_, initiator) = await RegisterAndVerifyAsync();
        var (_, suspended) = await RegisterAndVerifyAsync();

        await SuspendAsync(UserId.From(suspended.User.Id));

        Api.Authenticate(initiator.Tokens.AccessToken);

        var response = await OpenAsync(suspended.User.Id, "suspended-recipient");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var body = await response.Content.ReadAsStringAsync();

        // Another member's moderation state is theirs. A refusal that named it would make opening a
        // conversation a probe for who the platform has acted against.
        body.ToLowerInvariant().Should().NotContain("suspend");
    }

    /// <summary>
    /// The counterpart every refusal test needs: the ordinary case still works. A policy pair that
    /// refused everybody would pass both tests above.
    /// </summary>
    [Fact]
    public async Task Two_members_in_good_standing_are_unaffected()
    {
        var (_, initiator) = await RegisterAndVerifyAsync();
        var (_, recipient) = await RegisterAndVerifyAsync();

        Api.Authenticate(initiator.Tokens.AccessToken);

        (await OpenAsync(recipient.User.Id, "unencumbered")).StatusCode
            .Should().Be(HttpStatusCode.Created);
    }

    /// <summary>
    /// A member with no trust profile — everybody, until they first touch a trust feature — answers
    /// <see cref="AccountStanding.Unknown"/>, and that is not a refusal.
    /// </summary>
    [Fact]
    public async Task An_unprovisioned_member_has_an_unknown_standing_and_is_not_refused()
    {
        var (_, member) = await RegisterAndVerifyAsync();

        using var scope = Factory.Services.CreateScope();

        var standing = await scope.ServiceProvider
            .GetRequiredService<ITrustService>()
            .GetAccountStandingAsync(UserId.From(member.User.Id));

        standing.Should().Be(AccountStanding.Unknown);
    }

    // ── Fixtures ─────────────────────────────────────────────────────────

    private Task<HttpResponseMessage> OpenAsync(Guid recipientId, string entityId) =>
        HttpClient.PostAsJsonAsync(
            "/api/v1/conversations",
            new
            {
                recipientId,
                context = new
                {
                    type = "MarketplaceListing",
                    entityId,
                    entityTitle = "A listing"
                }
            },
            JsonOptions);

    private async Task SuspendAsync(UserId userId)
    {
        using var scope = Factory.Services.CreateScope();

        var provisioner = scope.ServiceProvider.GetRequiredService<TrustProfileProvisioner>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var profile = await provisioner.GetOrCreateAsync(userId);
        profile.Suspend("Community Guidelines.", clock.UtcNow);

        await unitOfWork.SaveChangesAsync();

        // The read path this asserts against is a no-tracking projection, so the write above has to
        // be visible to a fresh scope rather than to this one's change tracker.
        using var verification = Factory.Services.CreateScope();

        var stored = await verification.ServiceProvider
            .GetRequiredService<ITrustProfileRepository>()
            .GetByUserReadOnlyAsync(userId);

        stored.Should().NotBeNull();
    }
}
