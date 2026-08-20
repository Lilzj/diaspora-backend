using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Events;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Recommendations;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.UnitTests.Knowledge;

/// <summary>
/// What a curator says to read after one guide.
/// <para>
/// <b>An aggregate per source guide, and it is not the guide.</b> Recommendations are editorial
/// metadata about a guide, written by somebody who is usually not its author, changed on a
/// completely different rhythm from the content. Storing them on the guide would mean loading,
/// locking and bumping the revision of a published document every time an editor adjusted a
/// suggestion box under it — and would give the author an edit conflict caused by somebody else's
/// curation.
/// </para>
/// </summary>
public class KnowledgeRecommendationTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    private static readonly UserId Curator = UserId.From(Guid.NewGuid());

    [Fact]
    public void A_curated_list_keeps_the_order_it_was_given()
    {
        var (recommendation, _) = Start();

        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();

        recommendation.SetReads(
            [(first, "Do this next."), (second, null), (third, "Then this.")], Curator)
            .IsSuccess.Should().BeTrue();

        recommendation.Reads.Select(read => read.GuideId).Should().BeEquivalentTo(
            new[] { first, second, third }, options => options.WithStrictOrdering());

        recommendation.Reads.Select(read => read.Position).Should().BeEquivalentTo([0, 1, 2]);
    }

    /// <summary>
    /// The reason is what distinguishes a curated recommendation from a computed one, and it is why
    /// the curated version is worth building first.
    /// </summary>
    [Fact]
    public void A_curators_reason_is_kept_verbatim()
    {
        var (recommendation, _) = Start();

        recommendation.SetReads(
            [(Guid.NewGuid(), "Read this before you sign anything.")], Curator);

        recommendation.Reads[0].Reason.Should().Be("Read this before you sign anything.");
    }

    [Fact]
    public void A_guide_cannot_recommend_itself()
    {
        var (recommendation, source) = Start();

        recommendation.SetReads([(source.Value, null)], Curator)
            .Error.Should().Be(KnowledgeErrors.RecommendationSelf);
    }

    /// <summary>
    /// Collapsed rather than rejected: the curator meant it once, and failing the whole list over a
    /// duplicated paste helps nobody.
    /// </summary>
    [Fact]
    public void A_repeated_guide_collapses_rather_than_failing_the_list()
    {
        var (recommendation, _) = Start();

        var guide = Guid.NewGuid();

        recommendation.SetReads([(guide, "One"), (guide, "Two")], Curator)
            .IsSuccess.Should().BeTrue();

        recommendation.Reads.Should().ContainSingle();
        recommendation.Reads[0].Reason.Should().Be("One");
    }

    /// <summary>
    /// A curator sometimes genuinely means "nothing follows this", and must be able to say so
    /// without the record of who decided it disappearing.
    /// </summary>
    [Fact]
    public void An_empty_list_is_valid_and_keeps_the_aggregate()
    {
        var (recommendation, _) = Start();

        recommendation.SetReads([(Guid.NewGuid(), null)], Curator);
        recommendation.SetReads([], Curator).IsSuccess.Should().BeTrue();

        recommendation.Reads.Should().BeEmpty();
    }

    [Fact]
    public void More_next_reads_than_one_guide_can_recommend_are_refused()
    {
        var (recommendation, _) = Start();

        var many = Enumerable.Range(0, 20)
            .Select(_ => (Guid.NewGuid(), (string?)null))
            .ToList();

        recommendation.SetReads(many, Curator)
            .Error.Should().Be(KnowledgeErrors.RecommendationLimit);
    }

    [Fact]
    public void An_empty_guide_identifier_is_refused()
    {
        var (recommendation, _) = Start();

        recommendation.SetReads([(Guid.Empty, null)], Curator)
            .Error.Should().Be(KnowledgeErrors.GuideNotDiscoverable);
    }

    [Fact]
    public void A_change_raises_the_event_a_cache_invalidates_from()
    {
        var (recommendation, source) = Start();

        recommendation.SetReads([(Guid.NewGuid(), null), (Guid.NewGuid(), null)], Curator);

        var raised = recommendation.DomainEvents
            .OfType<RecommendedReadsChangedDomainEvent>()
            .Single();

        raised.SourceGuideId.Should().Be(source);
        raised.RecommendedCount.Should().Be(2);
        raised.ChangedBy.Should().Be(Curator);
    }

    [Fact]
    public void A_write_from_a_stale_revision_is_refused()
    {
        var (recommendation, _) = Start();

        recommendation.SetReads([(Guid.NewGuid(), null)], Curator);

        recommendation.EnsureRevision(0).Error.Should().Be(KnowledgeErrors.RevisionConflict);
        recommendation.EnsureRevision(recommendation.Revision).IsSuccess.Should().BeTrue();
    }

    private static (KnowledgeRecommendation Recommendation, GuideId Source) Start()
    {
        var source = GuideId.New();

        var recommendation = KnowledgeRecommendation.Create(
            KnowledgeRecommendationId.New(), source, Now).Value;

        return (recommendation, source);
    }
}
