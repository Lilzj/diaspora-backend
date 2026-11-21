using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Events;
using Diaspora.Contracts.Jobs.V1.Events;
using Diaspora.Modules.Jobs.Application.EventHandlers;
using Diaspora.Modules.Jobs.Domain.Applications;
using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Events;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Extensibility.Search;
using Diaspora.Platform.Messaging;
using Diaspora.UnitTests.Jobs.TestData;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Jobs;

/// <summary>
/// Deleting a posting, end to end through the chain BSOLID-005 found broken.
/// <para>
/// The aggregate raised <see cref="JobPostingDeletedDomainEvent"/> and the relay could translate
/// it; the handler between them had never been written, so a deletion reached neither the search
/// index nor any consumer. These tests hold each link of the chain separately, because a break in
/// any one of them is silent: an unhandled event commits normally, an uncalled public relay method
/// compiles, and a published contract with no consumer looks like one whose consumers are elsewhere.
/// </para>
/// </summary>
public class JobPostingDeletionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Marking_a_posting_deleted_announces_it_rather_than_vanishing_quietly()
    {
        var posting = JobFixtures.Posting();
        posting.ClearDomainEvents();

        posting.MarkDeleted(Now, "someone");

        var raised = posting.DomainEvents.OfType<JobPostingDeletedDomainEvent>().ToList();

        raised.Should().ContainSingle(
            "the aggregate sets the soft-delete fields itself rather than leaving it to the " +
            "change tracker, precisely so that there is an event for downstream de-indexing");
        raised[0].PostingId.Should().Be(posting.Id);
        raised[0].DeletedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Handling_the_deletion_takes_the_posting_out_of_search()
    {
        var search = Substitute.For<IJobSearchEngine>();
        var (handler, _) = Handler(search);
        var posting = Deleted();

        await Deliver(handler, posting);

        await search.Received(1).RemoveAsync(
            posting.Id.Value, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handling_the_deletion_announces_the_closure_to_other_modules()
    {
        var search = Substitute.For<IJobSearchEngine>();
        var (handler, publisher) = Handler(search);
        var posting = Deleted();

        await Deliver(handler, posting);

        await publisher.Received(1).PublishAsync(
            Arg.Is<JobClosedIntegrationEvent>(published =>
                published.JobId == posting.Id.Value && published.Reason == "withdrawn"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_search_provider_that_is_down_does_not_undo_the_delete()
    {
        var search = Substitute.For<IJobSearchEngine>();
        search.RemoveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("index unavailable")));

        var (handler, _) = Handler(search);

        var act = async () => await Deliver(handler, Deleted());

        await act.Should().NotThrowAsync(
            "the posting is already deleted by the time this runs, and throwing here would roll " +
            "back a completed delete because a search provider was briefly unavailable");
    }

    private static JobPosting Deleted()
    {
        var posting = JobFixtures.Posting();
        posting.MarkDeleted(Now, "someone");
        return posting;
    }

    private static (JobPostingDeletedEventHandler Handler, IIntegrationEventPublisher Publisher)
        Handler(IJobSearchEngine search)
    {
        var publisher = Substitute.For<IIntegrationEventPublisher>();

        var relay = new JobIntegrationEventRelay(
            publisher,
            Substitute.For<IJobPostingRepository>(),
            Substitute.For<IEmployerProfileRepository>(),
            Substitute.For<IJobApplicationRepository>(),
            NullLogger<JobIntegrationEventRelay>.Instance);

        return (
            new JobPostingDeletedEventHandler(
                relay, search, NullLogger<JobPostingDeletedEventHandler>.Instance),
            publisher);
    }

    private static Task Deliver(JobPostingDeletedEventHandler handler, JobPosting posting) =>
        handler.Handle(
            new DomainEventNotification<JobPostingDeletedDomainEvent>(
                posting.DomainEvents.OfType<JobPostingDeletedDomainEvent>().Single()),
            CancellationToken.None);
}
