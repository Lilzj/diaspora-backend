using Diaspora.Contracts.Providers.V1.Events;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Platform.Messaging.Outbox.Model;
using Diaspora.Platform.Messaging.Outbox.Partitioning;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Platform.Outbox;

/// <summary>
/// Where the platform pays for ordering.
/// <para>
/// The partition key is the unit of ordering: messages sharing one are delivered in write order
/// and never concurrently. Too coarse and one failing consumer stalls a whole module; too fine
/// and two events about the same provider arrive in the wrong order. These tests pin the
/// convention against the real contracts, because the convention is only correct in so far as
/// it matches what the shipped events actually look like.
/// </para>
/// </summary>
public sealed class OutboxPartitionKeyTests
{
    private static OutboxPartitionKeyResolver Resolver() =>
        new([new ConventionOutboxPartitionKeyStrategy()]);

    /// <summary>
    /// The case the whole scheme exists for: two events about one provider must be ordered
    /// against each other.
    /// </summary>
    [Fact]
    public void Two_events_about_the_same_provider_share_a_partition()
    {
        var resolver = Resolver();
        var providerId = Guid.NewGuid();

        var created = resolver.Resolve(new ProviderCreatedIntegrationEvent
        {
            ProviderId = providerId,
            OwnerId = Guid.NewGuid(),
            ProviderType = "Business",
            CategoryId = Guid.NewGuid(),
            Name = "A provider",
            Slug = "a-provider",
            CreatedAt = DateTimeOffset.UtcNow
        });

        var published = resolver.Resolve(new ProviderPublishedIntegrationEvent
        {
            ProviderId = providerId,
            OwnerId = Guid.NewGuid(),
            ProviderType = "Business",
            CategoryId = Guid.NewGuid(),
            Name = "A provider",
            Visibility = "Public",
            PublishedAt = DateTimeOffset.UtcNow
        });

        created.Should().Be(published);
    }

    /// <summary>
    /// And the other half: unrelated providers must not be ordered against each other, or one
    /// stuck consumer stalls every provider on the platform.
    /// </summary>
    [Fact]
    public void Events_about_different_providers_are_in_different_partitions()
    {
        var resolver = Resolver();

        var first = resolver.Resolve(NewProviderCreated(Guid.NewGuid()));
        var second = resolver.Resolve(NewProviderCreated(Guid.NewGuid()));

        first.Should().NotBe(second);
    }

    /// <summary>
    /// Subject nouns are tried longest-first and the first one the contract actually carries
    /// wins. This event names a recommendation but carries no <c>ProviderRecommendationId</c>,
    /// so it falls back to the provider — which is the right grain anyway: two recommendations
    /// for one provider are worth ordering, and one for another provider is not related.
    /// </summary>
    [Fact]
    public void The_narrowest_subject_the_contract_actually_carries_wins()
    {
        var providerId = Guid.NewGuid();

        var key = Resolver().Resolve(new ProviderRecommendationReceivedIntegrationEvent
        {
            RecommendationId = Guid.NewGuid(),
            ProviderId = providerId,
            RecommenderId = Guid.NewGuid(),
            Kind = "Endorsement",
            ReceivedAt = DateTimeOffset.UtcNow
        });

        key.Should().Be($"ProviderId:{providerId}");
    }

    /// <summary>
    /// The Trust contracts partition on the verification, so an approval can never overtake the
    /// submission it answers.
    /// </summary>
    [Fact]
    public void A_trust_verification_partitions_on_the_verification()
    {
        var verificationId = Guid.NewGuid();

        var key = Resolver().Resolve(new VerificationApprovedIntegrationEvent
        {
            VerificationId = verificationId,
            UserId = Guid.NewGuid(),
            VerificationType = "Identity",
            Partial = false,
            ApprovedAt = DateTimeOffset.UtcNow
        });

        key.Should().Be($"VerificationId:{verificationId}");
    }

    /// <summary>
    /// The fallback, and the reason it is what it is. Grouping unclassifiable events by type
    /// would queue every one of them behind every other, and it would look like it was working
    /// until one of them failed.
    /// </summary>
    [Fact]
    public void An_unclassifiable_event_is_ordered_against_nothing()
    {
        var resolver = Resolver();

        var first = resolver.Resolve(new OutboxUnpartitionableEvent { Description = "one" });
        var second = resolver.Resolve(new OutboxUnpartitionableEvent { Description = "two" });

        first.Should().NotBe(second);
        first.Should().StartWith("event:");
    }

    /// <summary>
    /// A strategy contributed by a module takes precedence over the convention, which is what
    /// makes the grain configurable without editing the default for everybody.
    /// </summary>
    [Fact]
    public void A_contributed_strategy_wins_over_the_convention()
    {
        var resolver = new OutboxPartitionKeyResolver(
            [new FixedOutboxPartitionKeyStrategy("everything-together"),
             new ConventionOutboxPartitionKeyStrategy()]);

        resolver.Resolve(NewProviderCreated(Guid.NewGuid())).Should().Be("everything-together");
    }

    /// <summary>
    /// The key must fit its column, and it must be truncated from the left: keys built from a
    /// long prefix differ only in their tail, and cutting the tail would map every one of them
    /// onto a single partition.
    /// </summary>
    [Fact]
    public void An_over_long_key_is_clamped_without_losing_what_distinguishes_it()
    {
        var prefix = new string('x', OutboxFieldLimits.PartitionKey);

        var resolver = new OutboxPartitionKeyResolver(
            [new FixedOutboxPartitionKeyStrategy(prefix + "-distinct-tail")]);

        var key = resolver.Resolve(new OutboxUnpartitionableEvent { Description = "any" });

        key.Length.Should().Be(OutboxFieldLimits.PartitionKey);
        key.Should().EndWith("-distinct-tail");
    }

    private static ProviderCreatedIntegrationEvent NewProviderCreated(Guid providerId) => new()
    {
        ProviderId = providerId,
        OwnerId = Guid.NewGuid(),
        ProviderType = "Business",
        CategoryId = Guid.NewGuid(),
        Name = "A provider",
        Slug = "a-provider",
        CreatedAt = DateTimeOffset.UtcNow
    };
}
