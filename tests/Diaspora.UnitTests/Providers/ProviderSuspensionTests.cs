using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Events;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Providers;

/// <summary>
/// A staff suspension ends only when staff lift it.
/// <para>
/// <b>The escape the audit found (7 Nov 2026):</b> archiving was allowed from Suspended, and
/// publishing is allowed from Archived, so an owner could archive a suspended provider and publish
/// it again without staff ever reinstating it. Every owner transition out of Suspended is now
/// refused by the aggregate.
/// </para>
/// </summary>
public class ProviderSuspensionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_suspended_provider_cannot_be_archived()
    {
        var provider = Suspended();

        var archived = provider.Archive(Now, "Taking it down myself.");

        archived.IsFailure.Should().BeTrue();
        archived.Error.Should().Be(ProviderErrors.Suspended);
        provider.Status.Should().Be(ProviderStatus.Suspended);
    }

    [Fact]
    public void No_owner_transition_leads_out_of_a_suspension()
    {
        var provider = Suspended();

        provider.Publish(Now).IsFailure.Should().BeTrue();
        provider.Pause(Now).IsFailure.Should().BeTrue();
        provider.Close(Now).IsFailure.Should().BeTrue();
        provider.Archive(Now).IsFailure.Should().BeTrue();

        provider.Status.Should().Be(ProviderStatus.Suspended);
        provider.StatusReason.Should().Be("Unlicensed money transmission.");
    }

    [Fact]
    public void Reinstating_returns_the_provider_and_carries_the_reason_on_the_event()
    {
        var provider = Suspended();

        provider.Reinstate(Now, reason: "Licence produced.").IsSuccess.Should().BeTrue();

        provider.Status.Should().Be(ProviderStatus.Active);
        provider.StatusReason.Should().BeNull("the suspension it explained is over");

        provider.DomainEvents.OfType<ProviderStatusChangedDomainEvent>()
            .Last().Reason.Should().Be("Licence produced.");
    }

    [Fact]
    public void An_owner_may_still_archive_and_republish_a_provider_that_is_not_suspended()
    {
        var provider = Published();

        provider.Archive(Now, "Closed for refurbishment.").IsSuccess.Should().BeTrue();
        provider.Publish(Now).IsSuccess.Should().BeTrue();

        provider.Status.Should().Be(ProviderStatus.Active);
    }

    private static Provider Published()
    {
        var provider = ProviderOwnershipTests.Register();

        var phone = ProviderContactMethod.Create(
            ProviderContactMethodId.New(), ContactMethodKind.Phone, "+447700900456");

        provider.AddContactMethod(phone.Value).IsSuccess.Should().BeTrue();
        provider.Publish(Now).IsSuccess.Should().BeTrue();

        return provider;
    }

    private static Provider Suspended()
    {
        var provider = Published();

        provider.Suspend(Now, "Unlicensed money transmission.").IsSuccess.Should().BeTrue();

        return provider;
    }
}
