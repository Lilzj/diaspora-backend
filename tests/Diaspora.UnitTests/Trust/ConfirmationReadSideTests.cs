using Diaspora.Modules.Trust.Application.Reputation;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Trust;

/// <summary>
/// The read side of a write side that shipped without one.
/// <para>
/// <c>POST /trust/contributions/confirmations</c> has recorded confirmations since the module
/// shipped and nothing could read them back: <c>CountForTargetAsync</c> was written and never
/// called from anywhere. Every surface offering a confirm control therefore had to invent its
/// count or borrow one from another module.
/// </para>
/// </summary>
public sealed class ConfirmationReadSideTests
{
    private static readonly UserId Viewer =
        UserId.From(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"));

    private const string TargetType = "guide";
    private const string TargetId = "guide-42";

    [Fact]
    public async Task The_tally_and_the_viewers_own_state_come_back_together()
    {
        var handler = Handler(count: 7, confirmedByViewer: true);

        var result = await handler.Handle(Query(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ConfirmationCount.Should().Be(7);

        // Without this the control cannot render as spent, and a member finds out they already
        // confirmed by being refused.
        result.Value.ConfirmedByViewer.Should().BeTrue();
    }

    /// <summary>
    /// The control has to render before the first confirmation exists, or nobody can ever give
    /// it one.
    /// </summary>
    [Fact]
    public async Task A_target_nobody_has_confirmed_is_a_zero_rather_than_a_failure()
    {
        var handler = Handler(count: 0, confirmedByViewer: false);

        var result = await handler.Handle(Query(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ConfirmationCount.Should().Be(0);
        result.Value.ConfirmedByViewer.Should().BeFalse();
    }

    /// <summary>
    /// The aggregate lower-cases the target type on write, so a read that did not would answer
    /// zero for a caller that spelled it with capitals.
    /// </summary>
    [Fact]
    public async Task The_target_type_comes_back_in_the_casing_the_write_side_stores()
    {
        var handler = Handler(count: 3, confirmedByViewer: false);

        var result = await handler.Handle(
            new GetConfirmationSummaryQuery { TargetType = "  Guide  ", TargetId = TargetId },
            CancellationToken.None);

        result.Value.TargetType.Should().Be("guide");
    }

    [Fact]
    public async Task An_unauthenticated_caller_is_refused_rather_than_served_a_viewer_flag()
    {
        var contributions = Substitute.For<ICommunityContributionRepository>();
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns((UserId?)null);

        var handler = new GetConfirmationSummaryQueryHandler(contributions, currentUser);

        var result = await handler.Handle(Query(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    private static GetConfirmationSummaryQuery Query() =>
        new() { TargetType = TargetType, TargetId = TargetId };

    private static GetConfirmationSummaryQueryHandler Handler(int count, bool confirmedByViewer)
    {
        var contributions = Substitute.For<ICommunityContributionRepository>();

        contributions
            .CountForTargetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(count);

        contributions
            .ExistsAsync(
                Arg.Any<UserId>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(confirmedByViewer);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(Viewer);

        return new GetConfirmationSummaryQueryHandler(contributions, currentUser);
    }
}
