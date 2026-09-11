using Diaspora.Contracts.Automation.V1.Extensibility;
using Diaspora.Modules.Automation.Application.Services;
using Diaspora.Modules.Automation.Domain.Catalog;
using Diaspora.Modules.Automation.Options;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Automation;

/// <summary>
/// "Tell me" on a group's automation tells the member who wrote it — 11 Sep 2026.
/// <para>
/// <c>AutomationActionDispatcher.ResolveOwnerRecipient</c> was written for this: "a community
/// automation's <c>notify.self</c> means the moderator who wrote it". The catalogue served
/// <c>notify.self</c> to the six business kinds only, so the validator refused it on any group's
/// rule and the dispatcher's branch for groups could not be reached. The founder of a community who
/// built "tell me when it reaches a milestone" found that out when they pressed Save.
/// </para>
/// </summary>
public sealed class AutomationGroupAuthorTests
{
    private static readonly AutomationRuleValidator Validator =
        new(Options.Create(new AutomationOptions()));

    [Theory]
    [InlineData(AutomationOwnerKind.Community)]
    [InlineData(AutomationOwnerKind.Organization)]
    public void Tell_me_is_served_to_a_group_whose_automation_has_an_author(AutomationOwnerKind owner) =>
        AutomationCatalog.FindAction("notify.self")!.AllowedOwners.Should().Contain(owner);

    [Fact]
    public void A_founder_can_be_told_when_their_community_reaches_a_milestone()
    {
        var result = Validator.Validate(
            "community.milestoneReached",
            null,
            [],
            [new ActionInput(null, "notify.self", "{}")],
            AutomationOwnerKind.Community,
            ["automations.community"],
            activating: true);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Description : string.Empty);
    }

    [Fact]
    public void An_administrator_can_be_told_about_a_registration_without_telling_every_member()
    {
        var result = Validator.Validate(
            "organization.registrationReceived",
            null,
            [],
            [new ActionInput(null, "notify.self", "{}")],
            AutomationOwnerKind.Organization,
            ["automations.organization"],
            activating: true);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Description : string.Empty);
    }

    [Fact]
    public void The_other_personal_actions_stay_with_the_business_kinds()
    {
        // A push or an email on a group's behalf is a question nobody has asked yet; widening
        // "tell me" is not an answer to it.
        foreach (var key in new[] { "notify.push", "notify.email" })
        {
            AutomationCatalog.FindAction(key)!.AllowedOwners
                .Should().NotContain(AutomationOwnerKind.Community)
                .And.NotContain(AutomationOwnerKind.Organization);
        }
    }

    [Fact]
    public void Platform_is_still_nobody_s()
    {
        AutomationCatalog.FindAction("notify.self")!.AllowedOwners
            .Should().NotContain(AutomationOwnerKind.Platform);
    }
}
