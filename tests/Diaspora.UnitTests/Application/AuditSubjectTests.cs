using Diaspora.Application.Behaviors;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Application;

/// <summary>
/// What the audit trail records as the thing acted on, and the reason given (7 Nov 2026: the entity
/// id was always empty and no reason was kept).
/// </summary>
public sealed class AuditSubjectTests
{
    private sealed record HideSomething
    {
        public Guid CorrelationId { get; init; } = Guid.NewGuid();
        public required Guid PostingId { get; init; }
        public required ModerationBody Request { get; init; }
    }

    private sealed record ModerationBody
    {
        public required string Action { get; init; }
        public string? Reason { get; init; }
    }

    private sealed record DecideSomething
    {
        public required DecisionBody Request { get; init; }
    }

    private sealed record DecisionBody
    {
        public required string ContentType { get; init; }
        public required Guid ContentId { get; init; }
        public string? Reason { get; init; }
    }

    private sealed record SuspendSomeone
    {
        public required UserId UserId { get; init; }
        public required string Reason { get; init; }
    }

    private sealed record CreatedThing(Guid ThingId, string Name);

    [Fact]
    public void The_target_is_the_commands_own_id_and_never_its_correlation_id()
    {
        var posting = Guid.NewGuid();

        var request = new HideSomething
        {
            PostingId = posting,
            Request = new ModerationBody { Action = "Hide", Reason = "  Asks for a fee.  " }
        };

        AuditSubject.EntityIdOf(request).Should().Be(posting.ToString());
        AuditSubject.ReasonOf(request).Should().Be("Asks for a fee.", "the reason sits in the HTTP body");
    }

    [Fact]
    public void A_command_that_only_wraps_its_body_is_read_through_the_body()
    {
        var content = Guid.NewGuid();

        var request = new DecideSomething
        {
            Request = new DecisionBody { ContentType = "job", ContentId = content, Reason = "Scam." }
        };

        AuditSubject.EntityIdOf(request).Should().Be(content.ToString());
        AuditSubject.ReasonOf(request).Should().Be("Scam.");
    }

    [Fact]
    public void A_strongly_typed_id_is_recorded_as_its_guid_and_not_as_a_record_printout()
    {
        var user = Guid.NewGuid();

        var request = new SuspendSomeone { UserId = UserId.From(user), Reason = "Fraud." };

        AuditSubject.EntityIdOf(request).Should().Be(user.ToString());
        AuditSubject.EntityIdOf(request).Should().NotContain("Value =");
    }

    [Fact]
    public void A_blank_reason_is_no_reason()
    {
        var request = new HideSomething
        {
            PostingId = Guid.NewGuid(),
            Request = new ModerationBody { Action = "Unhide", Reason = "   " }
        };

        AuditSubject.ReasonOf(request).Should().BeNull();
    }

    [Fact]
    public void A_create_that_names_nothing_is_recorded_against_what_it_created()
    {
        var thing = Guid.NewGuid();

        AuditSubject.EntityIdOfResult(Result.Success(new CreatedThing(thing, "New"))).Should().Be(thing.ToString());
        AuditSubject.EntityIdOfResult(Result.Success()).Should().BeNull();
    }
}
