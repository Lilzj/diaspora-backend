using Diaspora.Modules.Community.Application.Moderation;
using Diaspora.Modules.Community.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Application.Moderation;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Application.Moderation;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Marketplace.Application.Moderation;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;
using FluentAssertions;
using FluentValidation;
using Xunit;

namespace Diaspora.UnitTests.Moderation;

/// <summary>
/// Every module's moderation route needs a reason for every action, including lifting one
/// (7 Nov 2026). Housing never asked; community life and Community's cross-module route took it as
/// optional; marketplace asked only to take something down. Jobs moderation is a Trust case decision
/// since stage 5, where the note is required by the case itself.
/// </summary>
public sealed class ModerationReasonRequiredTests
{
    public static TheoryData<string?> Blank => new() { null, "", "   " };

    [Theory]
    [MemberData(nameof(Blank))]
    public void Housing_refuses_a_moderation_action_with_no_reason(string? reason) =>
        Refuses(new ModerateHousingCommandValidator(), new ModerateHousingCommand
        {
            ListingId = Guid.NewGuid(),
            Request = new ModerateHousingRequest { Action = "clear", Reason = reason }
        });

    [Theory]
    [MemberData(nameof(Blank))]
    public void Community_life_refuses_a_moderation_action_with_no_reason(string? reason) =>
        Refuses(new ModerateSubjectCommandValidator(), new ModerateSubjectCommand
        {
            SubjectType = "events",
            SubjectId = Guid.NewGuid(),
            Request = new ModerateSubjectRequest { Action = "hide", Reason = reason }
        });

    [Theory]
    [MemberData(nameof(Blank))]
    public void Communitys_cross_module_decision_refuses_no_reason(string? reason) =>
        Refuses(new DecideModerationCommandValidator(), new DecideModerationCommand
        {
            Request = new ModerationDecisionRequest
            {
                ContentType = "job",
                ContentId = Guid.NewGuid(),
                Action = "hide",
                Reason = reason
            }
        });

    [Theory]
    [MemberData(nameof(Blank))]
    public void Marketplace_refuses_even_a_restore_with_no_reason(string? reason) =>
        Refuses(new ModerateListingCommandValidator(), new ModerateListingCommand
        {
            ListingId = Guid.NewGuid(),
            Request = new ModerateListingRequest { Action = "restore", Reason = reason }
        });

    [Fact]
    public void A_written_reason_is_accepted()
    {
        new ModerateHousingCommandValidator().Validate(new ModerateHousingCommand
        {
            ListingId = Guid.NewGuid(),
            Request = new ModerateHousingRequest { Action = "hide", Reason = "Duplicate of another advert." }
        }).IsValid.Should().BeTrue();
    }

    private static void Refuses<T>(IValidator<T> validator, T command)
    {
        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage.Contains("reason is required"));
    }
}
