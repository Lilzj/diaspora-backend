using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.CommunityProgramme;

/// <summary>
/// A community's initiatives — 21 Oct 2026. What an initiative refuses to store, the only ways its
/// status moves, and when it stops taking participants and updates.
/// </summary>
public class CommunityInitiativeDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 21);
    private static readonly UserId Organiser = UserId.New();

    [Fact]
    public void An_initiative_keeps_trimmed_text_and_starts_planned_with_its_organisers()
    {
        var initiative = Create(title: "  Winter coats ", description: " Collect and hand out. ").Value;

        initiative.Title.Should().Be("Winter coats");
        initiative.Description.Should().Be("Collect and hand out.");
        initiative.Status.Should().Be(CommunityInitiativeStatus.Planned);
        initiative.StartedAt.Should().BeNull();
        initiative.IsOrganiser(Organiser).Should().BeTrue();
        initiative.AcceptsParticipants.Should().BeTrue();
    }

    [Fact]
    public void An_initiative_created_active_records_when_it_started()
    {
        var initiative = Create(status: CommunityInitiativeStatus.Active).Value;

        initiative.StartedAt.Should().Be(Now);
    }

    [Theory]
    [InlineData(CommunityInitiativeStatus.Completed)]
    [InlineData(CommunityInitiativeStatus.Cancelled)]
    public void Nothing_is_created_finished(CommunityInitiativeStatus status)
    {
        var created = Create(status: status);

        created.IsFailure.Should().BeTrue();
        created.Error.Code.Should().EndWith("initiative.status.create");
    }

    [Theory]
    [InlineData(null, "Description", "initiative.title.required")]
    [InlineData("Title", " ", "initiative.description.required")]
    public void An_initiative_without_a_title_or_description_is_refused(string? title, string? description, string code)
    {
        var created = Create(title: title, description: description);

        created.IsFailure.Should().BeTrue();
        created.Error.Code.Should().EndWith(code);
    }

    [Fact]
    public void An_initiative_needs_an_organiser_and_at_most_ten()
    {
        Create(organisers: []).Error.Code.Should().EndWith("initiative.organiser.required");

        var eleven = Enumerable.Range(0, 11).Select(_ => UserId.New()).ToList();
        Create(organisers: eleven).Error.Code.Should().EndWith("initiative.organiser.too-many");
    }

    [Fact]
    public void A_target_date_in_the_past_is_refused_but_today_is_not()
    {
        Create(targetDate: Today.AddDays(-5)).Error.Code.Should().EndWith("initiative.target-date.in-past");
        Create(targetDate: Today).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void A_revision_keeps_what_it_does_not_send_and_can_clear_the_date()
    {
        var initiative = Create(targetDate: Today.AddDays(30)).Value;

        initiative.Revise(null, "New plan.", null, clearTargetDate: false, Now).IsSuccess.Should().BeTrue();
        initiative.Title.Should().Be("Winter coats");
        initiative.Description.Should().Be("New plan.");
        initiative.TargetDate.Should().Be(Today.AddDays(30));

        initiative.Revise(null, null, null, clearTargetDate: true, Now).IsSuccess.Should().BeTrue();
        initiative.TargetDate.Should().BeNull();

        initiative.Revise(null, null, Today.AddDays(3), clearTargetDate: true, Now)
            .Error.Code.Should().EndWith("initiative.target-date.conflict");
    }

    [Fact]
    public void A_late_initiative_can_be_corrected_without_its_own_date_refusing_the_edit()
    {
        var initiative = Create(targetDate: Today.AddDays(2)).Value;

        // A month later the date is in the past; a text correction does not touch it.
        initiative.Revise("Still going", null, Today.AddDays(2), clearTargetDate: false, Now.AddDays(30))
            .IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(CommunityInitiativeStatus.Planned, CommunityInitiativeStatus.Active, true)]
    [InlineData(CommunityInitiativeStatus.Planned, CommunityInitiativeStatus.Cancelled, true)]
    [InlineData(CommunityInitiativeStatus.Planned, CommunityInitiativeStatus.Completed, false)]
    [InlineData(CommunityInitiativeStatus.Active, CommunityInitiativeStatus.Completed, true)]
    [InlineData(CommunityInitiativeStatus.Active, CommunityInitiativeStatus.Cancelled, true)]
    [InlineData(CommunityInitiativeStatus.Active, CommunityInitiativeStatus.Planned, false)]
    public void Status_moves_only_forward(
        CommunityInitiativeStatus from, CommunityInitiativeStatus to, bool allowed)
    {
        var initiative = Create(status: from).Value;

        var moved = initiative.ChangeStatus(to, Now);

        moved.IsSuccess.Should().Be(allowed);

        if (!allowed)
        {
            moved.Error.Code.Should().EndWith("initiative.status.transition");
            initiative.Status.Should().Be(from);
        }
    }

    [Theory]
    [InlineData(CommunityInitiativeStatus.Completed)]
    [InlineData(CommunityInitiativeStatus.Cancelled)]
    public void A_closed_initiative_is_not_reopened_rewritten_or_joined(CommunityInitiativeStatus end)
    {
        var initiative = Create(status: CommunityInitiativeStatus.Active).Value;
        initiative.ChangeStatus(end, Now).IsSuccess.Should().BeTrue();

        initiative.ClosedAt.Should().Be(Now);
        initiative.ChangeStatus(CommunityInitiativeStatus.Active, Now).IsFailure.Should().BeTrue();
        initiative.Revise("New", null, null, false, Now).Error.Code.Should().EndWith("initiative.closed");

        var joined = CommunityInitiativeParticipant.Join(initiative, UserId.New(), Now);
        joined.IsFailure.Should().BeTrue();
        joined.Error.Code.Should().EndWith("initiative.join.closed");
    }

    [Fact]
    public void Asking_for_the_status_it_already_has_changes_nothing()
    {
        var initiative = Create(status: CommunityInitiativeStatus.Active).Value;

        initiative.ChangeStatus(CommunityInitiativeStatus.Active, Now.AddDays(1)).IsSuccess.Should().BeTrue();
        initiative.StartedAt.Should().Be(Now);
    }

    [Fact]
    public void A_completed_initiative_still_takes_a_closing_update_and_a_cancelled_one_does_not()
    {
        var completed = Create(status: CommunityInitiativeStatus.Active).Value;
        completed.ChangeStatus(CommunityInitiativeStatus.Completed, Now);

        CommunityInitiativeUpdate.Post(completed, Organiser, "Done", "We handed out 200 coats.", Now)
            .IsSuccess.Should().BeTrue();

        var cancelled = Create().Value;
        cancelled.ChangeStatus(CommunityInitiativeStatus.Cancelled, Now);

        CommunityInitiativeUpdate.Post(cancelled, Organiser, "Late", "Too late.", Now)
            .Error.Code.Should().EndWith("initiative.update.closed");
    }

    [Theory]
    [InlineData(null, "Body", "initiative.update.title.required")]
    [InlineData("Title", "", "initiative.update.body.required")]
    public void An_update_without_a_title_or_body_is_refused(string? title, string? body, string code)
    {
        var posted = CommunityInitiativeUpdate.Post(Create().Value, Organiser, title, body, Now);

        posted.IsFailure.Should().BeTrue();
        posted.Error.Code.Should().EndWith(code);
    }

    private static Diaspora.SharedKernel.Results.Result<CommunityInitiative> Create(
        string? title = "Winter coats",
        string? description = "Collect coats and hand them out before December.",
        DateOnly? targetDate = null,
        CommunityInitiativeStatus status = CommunityInitiativeStatus.Planned,
        IReadOnlyCollection<UserId>? organisers = null) =>
        CommunityInitiative.Create(
            CommunityInitiativeId.New(),
            CommunityId.New(),
            Organiser,
            title,
            description,
            targetDate,
            status,
            organisers ?? [Organiser],
            Now);
}
