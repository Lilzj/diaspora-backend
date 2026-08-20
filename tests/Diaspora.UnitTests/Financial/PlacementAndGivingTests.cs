using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Placements;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.UnitTests.Financial.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Financial;

/// <summary>
/// Purchased placement, and giving that is usually not money.
/// </summary>
public class PlacementAndGivingTests
{
    // ── Placements ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_placement_starts_scheduled_and_announces_nothing_until_it_activates()
    {
        var placement = Placement_();

        placement.Status.Should().Be(PlacementStatus.Scheduled);
        placement.DomainEvents.Should().BeEmpty(
            "a placement activated on purchase is one somebody keeps when the payment later fails");
    }

    [Fact]
    public void Activating_a_placement_puts_it_in_force_and_announces_it()
    {
        var placement = Placement_();

        placement.Activate(FinancialTestData.Now);

        placement.IsInForce(FinancialTestData.Now).Should().BeTrue();
        placement.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    public void A_placement_whose_window_has_closed_reports_that_it_has_lapsed()
    {
        var placement = Placement_();

        placement.Activate(FinancialTestData.Now);

        placement.HasLapsed(FinancialTestData.Now.AddDays(8)).Should().BeTrue();
        placement.IsInForce(FinancialTestData.Now.AddDays(8)).Should().BeFalse();
    }

    [Fact]
    public void Ending_a_placement_announces_which_kind_of_ending_it_was()
    {
        var placement = Placement_();

        placement.Activate(FinancialTestData.Now);
        placement.ClearDomainEvents();
        placement.Cancel("The listing was archived.", FinancialTestData.Now);

        placement.Status.Should().Be(PlacementStatus.Cancelled);
        placement.DomainEvents.Should().ContainSingle();
    }

    /// <summary>At-least-once delivery means a retirement consumer may run twice.</summary>
    [Fact]
    public void Ending_an_already_ended_placement_does_nothing()
    {
        var placement = Placement_();

        placement.Activate(FinancialTestData.Now);
        placement.Cancel("First.", FinancialTestData.Now);
        placement.ClearDomainEvents();
        placement.Cancel("Second.", FinancialTestData.Now);

        placement.EndReason.Should().Be("First.");
        placement.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Extending_a_placement_moves_the_window_out_from_where_it_ended()
    {
        var placement = Placement_();

        placement.Activate(FinancialTestData.Now);
        placement.Extend(7, FinancialTestData.Now);

        placement.Window.EndsAt.Should().Be(FinancialTestData.Now.AddDays(14));
    }

    /// <summary>A subject is a type and a GUID; this module never resolves one.</summary>
    [Fact]
    public void A_subject_reference_is_a_type_and_an_identifier_and_nothing_else()
    {
        var subject = SubjectReference.Create(MonetizationSubjectType.Job, Guid.NewGuid());

        subject.Matches(MonetizationSubjectType.Job, subject.SubjectId).Should().BeTrue();
        subject.Matches(MonetizationSubjectType.Provider, subject.SubjectId).Should().BeFalse();
    }

    // ── Giving ───────────────────────────────────────────────────────────────────────────────

    /// <summary>Not hidden behind a flag — never stored.</summary>
    [Fact]
    public void An_anonymous_donation_stores_no_donor_identifier()
    {
        var donation = Donation.Pledge(
            DonationId.New(),
            Guid.NewGuid(),
            isAnonymous: true,
            BeneficiaryType.Fund,
            FinancialTestData.Gbp_(25m),
            "DON-20260805-7KQ4M2",
            FinancialTestData.Now);

        donation.DonorUserId.Should().BeNull();
        donation.IsAnonymous.Should().BeTrue();
    }

    [Fact]
    public void A_named_donation_keeps_its_donor()
    {
        var donor = Guid.NewGuid();

        var donation = Donation.Pledge(
            DonationId.New(),
            donor,
            isAnonymous: false,
            BeneficiaryType.Organization,
            FinancialTestData.Gbp_(25m),
            "DON-20260805-7KQ4M3",
            FinancialTestData.Now);

        donation.DonorUserId.Should().Be(donor);
    }

    [Fact]
    public void A_donation_of_nothing_is_refused()
    {
        var pledge = () => Donation.Pledge(
            DonationId.New(),
            Guid.NewGuid(),
            false,
            BeneficiaryType.Platform,
            FinancialTestData.Gbp_(0m),
            "DON-X",
            FinancialTestData.Now);

        pledge.Should().Throw<DomainException>().WithMessage("*not a donation*");
    }

    /// <summary>
    /// Three of the five support kinds are not money, and forcing them through an amount would have
    /// been the easy modelling mistake.
    /// </summary>
    [Fact]
    public void A_pledged_contribution_carries_a_quantity_and_no_amount()
    {
        var contribution = CommunityContribution.Pledged(
            CommunityContributionId.New(),
            ContributionFundId.New(),
            "community.fund",
            Guid.NewGuid(),
            isAnonymous: false,
            ContributionKind.Goods,
            PledgeDetail.Create(3m, "boxes", "School books"),
            "CON-20260805-ABCDEF",
            FinancialTestData.Now);

        contribution.Amount.Should().BeNull();
        contribution.IsMonetary.Should().BeFalse();
        contribution.Pledge!.Quantity.Should().Be(3m);
        contribution.Pledge.Unit.Should().Be("boxes");
    }

    [Fact]
    public void A_pledged_contribution_completes_without_a_transaction()
    {
        var contribution = CommunityContribution.Pledged(
            CommunityContributionId.New(),
            ContributionFundId.New(),
            "community.fund",
            null,
            isAnonymous: true,
            ContributionKind.VolunteerService,
            PledgeDetail.Create(6m, "hours", "Saturday mornings"),
            "CON-20260805-ABCDEG",
            FinancialTestData.Now);

        contribution.Complete(FinancialTestData.Now);

        contribution.Status.Should().Be(GivingStatus.Completed);
        contribution.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    public void A_monetary_contribution_will_not_complete_without_a_transaction()
    {
        var contribution = CommunityContribution.Monetary(
            CommunityContributionId.New(),
            ContributionFundId.New(),
            "community.fund",
            Guid.NewGuid(),
            false,
            FinancialTestData.Gbp_(25m),
            "CON-20260805-ABCDEH",
            FinancialTestData.Now);

        var complete = () => contribution.Complete(FinancialTestData.Now);

        complete.Should().Throw<DomainException>().WithMessage("*against a transaction*");
    }

    [Fact]
    public void A_pledge_cannot_be_recorded_as_a_monetary_contribution()
    {
        var pledged = () => CommunityContribution.Pledged(
            CommunityContributionId.New(),
            ContributionFundId.New(),
            "community.fund",
            Guid.NewGuid(),
            false,
            ContributionKind.Monetary,
            PledgeDetail.Create(1m, "items", string.Empty),
            "CON-X",
            FinancialTestData.Now);

        pledged.Should().Throw<DomainException>().WithMessage("*needs an amount*");
    }

    /// <summary>A coat drive that has raised nothing and gathered forty volunteers is a success.</summary>
    [Fact]
    public void A_cause_counts_every_supporter_and_totals_only_the_money()
    {
        var cause = DonationCause.Open(
            DonationCauseId.New(),
            "winter.coats",
            "Refugee Winter Clothing Drive",
            "Warm clothes for newly arrived refugees.",
            BeneficiaryType.Organization,
            "South London Diaspora Community Centre",
            "GBP",
            FinancialTestData.Now);

        cause.SetAcceptedKinds([ContributionKind.Goods, ContributionKind.VolunteerService]);

        cause.RecordDonation(null, FinancialTestData.Now);
        cause.RecordDonation(null, FinancialTestData.Now);

        cause.SupporterCount.Should().Be(2);
        cause.RaisedMinorUnits.Should().Be(0);
        cause.Accepts(ContributionKind.Monetary).Should().BeFalse();
        cause.Accepts(ContributionKind.Goods).Should().BeTrue();
    }

    [Fact]
    public void A_cause_refuses_a_donation_in_another_currency()
    {
        var cause = DonationCause.Open(
            DonationCauseId.New(),
            "bursary",
            "University Student Bursary Fund",
            "Tuition and rent for diaspora students.",
            BeneficiaryType.Organization,
            "Nigerian Students Union UK",
            "GBP",
            FinancialTestData.Now);

        var record = () => cause.RecordDonation(
            Money.FromAmount(25m, "USD"), FinancialTestData.Now);

        record.Should().Throw<DomainException>().WithMessage("*cannot be totalled into*");
    }

    private static Placement Placement_() =>
        Placement.Purchase(
            PlacementId.New(),
            SubjectReference.Create(MonetizationSubjectType.MarketplaceListing, Guid.NewGuid()),
            PlacementKind.Boost,
            DateWindow.ForDays(FinancialTestData.Now, 7),
            1.5m,
            Guid.NewGuid(),
            "marketplace.listing.boost.7d",
            FinancialTestData.Now);
}
