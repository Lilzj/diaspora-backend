using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Events;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Intelligence;

/// <summary>
/// The notice category catalogue and the register of issuing authorities.
/// <para>
/// <b>The category's alert family is the reason it is an aggregate rather than an enum.</b> A
/// deployment adding "Cyclone Warnings" needs the platform to treat it as an emergency, and a
/// hard-coded list inside the relay would mean it does not — a fact the deployment discovers during a
/// cyclone.
/// </para>
/// </summary>
public class IntelligenceCatalogueTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    private static readonly UserId Administrator = UserId.From(Guid.NewGuid());

    // ── Notice categories ───────────────────────────────────────────────────────────────────

    [Fact]
    public void A_created_category_normalises_its_key_and_is_active()
    {
        var category = Category("  Emergency-Alerts  ", "Emergency Alerts");

        category.Key.Should().Be("emergency-alerts");
        category.IsActive.Should().BeTrue();
        category.DefaultSeverity.Should().Be(NoticeSeverity.Critical);
        category.AlertFamily.Should().Be(NoticeAlertFamily.Emergency);
    }

    [Theory]
    [InlineData("Not A Key")]
    [InlineData("double--hyphen")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    [InlineData("")]
    public void An_unusable_key_is_refused(string key)
    {
        NoticeCategory.Create(NoticeCategoryId.New(), key, "Name", Now)
            .Error.Should().Be(IntelligenceErrors.CategoryKeyInvalid);
    }

    /// <summary>
    /// The alert family is what the relay branches on. A consumer that subscribed to emergencies
    /// hears about a category the deployment reclassified, without a release of this module.
    /// </summary>
    [Fact]
    public void A_category_can_be_reclassified_into_the_emergency_family()
    {
        var category = Category("cyclone-warnings", "Cyclone Warnings", NoticeAlertFamily.None);

        category.AlertFamily.Should().Be(NoticeAlertFamily.None);

        category.Update(
            "Cyclone Warnings",
            null,
            "🌀",
            10,
            NoticeSeverity.Critical,
            NoticeAlertFamily.Emergency,
            Administrator);

        category.AlertFamily.Should().Be(NoticeAlertFamily.Emergency);
        category.DefaultSeverity.Should().Be(NoticeSeverity.Critical);
    }

    [Fact]
    public void The_change_event_carries_the_key_a_preference_screen_stores()
    {
        var category = Category("visa-advisories", "Visa Advisories");

        var raised = category.DomainEvents
            .OfType<NoticeCategoryChangedDomainEvent>()
            .Single();

        raised.Key.Should().Be("visa-advisories");
        raised.Change.Should().Be(IntelligenceChangeKinds.Created);
        raised.IsActive.Should().BeTrue();
        raised.DefaultSeverity.Should().Be(NoticeSeverity.Critical);
    }

    [Fact]
    public void Archiving_is_the_soft_delete_and_restoring_undoes_it()
    {
        var category = Category("visa-advisories", "Visa Advisories");

        category.MarkDeleted(Now, Administrator.Value.ToString());

        category.IsDeleted.Should().BeTrue();
        category.IsActive.Should().BeFalse();

        category.Restore();

        category.IsDeleted.Should().BeFalse();
        category.IsActive.Should().BeTrue();
    }

    [Fact]
    public void A_category_label_falls_back_through_the_language_to_the_default()
    {
        var category = Category("visa-advisories", "Visa Advisories");

        category.SetLocalizations([("fr", "Avis de visa", "Les règles")], Administrator);

        category.Localized("fr").Name.Should().Be("Avis de visa");
        category.Localized("fr-MU").Name.Should().Be("Avis de visa");
        category.Localized("de").Name.Should().Be("Visa Advisories");
    }

    // ── Issuing authorities ─────────────────────────────────────────────────────────────────

    [Fact]
    public void An_authority_starts_unverified()
    {
        var authority = Authority();

        authority.IsVerified.Should().BeFalse(
            "the badge is Trust's decision, and nothing here can set it");
        authority.VerificationCaseId.Should().BeNull();
    }

    [Fact]
    public void An_authority_issues_only_for_its_own_country()
    {
        var authority = Authority();

        authority.IssuesFor("MU").Should().BeTrue();
        authority.IssuesFor("mu").Should().BeTrue("the comparison normalises");
        authority.IssuesFor("NG").Should().BeFalse();
        authority.IssuesFor(null).Should().BeFalse();
    }

    /// <summary>
    /// <b>The case identifier is the whole Trust integration.</b> B8 shipped its equivalent without
    /// storing one: submissions went out, Trust decided, and every approval had nowhere to land.
    /// </summary>
    [Fact]
    public void The_verification_case_is_stored_and_an_empty_one_is_ignored()
    {
        var authority = Authority();
        var caseId = Guid.NewGuid();

        authority.RecordVerificationCase(caseId);
        authority.VerificationCaseId.Should().Be(caseId);

        authority.RecordVerificationCase(Guid.Empty);
        authority.VerificationCaseId.Should().Be(caseId, "an empty case must not clear a real one");
    }

    [Fact]
    public void Applying_a_verification_raises_a_verification_verb_rather_than_an_update()
    {
        var authority = Authority();

        authority.ClearDomainEvents();
        authority.ApplyVerification(verified: true, Now);

        authority.DomainEvents
            .OfType<IssuingAuthorityChangedDomainEvent>()
            .Single()
            .Change
            .Should().Be(IntelligenceChangeKinds.Verified);

        authority.ClearDomainEvents();
        authority.ApplyVerification(verified: false, Now);

        authority.DomainEvents
            .OfType<IssuingAuthorityChangedDomainEvent>()
            .Single()
            .Change
            .Should().Be(IntelligenceChangeKinds.Rejected);

        authority.VerifiedAt.Should().BeNull();
    }

    [Fact]
    public void An_unusable_website_is_refused_and_contact_details_are_normalised()
    {
        var authority = Authority();

        authority.Update(
            "Passport and Immigration Office",
            "PIO",
            AuthorityType.ImmigrationDepartment,
            null,
            "javascript:alert(1)",
            null,
            null,
            Administrator).Error.Should().Be(IntelligenceErrors.SourceUrlInvalid);

        authority.Update(
            "Passport and Immigration Office",
            "PIO",
            AuthorityType.ImmigrationDepartment,
            null,
            "https://PIO.GOVMU.ORG/Forms?A=1",
            "  INFO@pio.govmu.org ",
            " +230 (210) 9312 ",
            Administrator).IsSuccess.Should().BeTrue();

        authority.ContactEmail.Should().Be("info@pio.govmu.org");
        authority.ContactPhone.Should().Be("+230 (210) 9312");
        authority.Website.Should().StartWith("https://pio.govmu.org/Forms?A=1",
            "the host lower-cases and the query string does not");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private static NoticeCategory Category(
        string key,
        string name,
        NoticeAlertFamily family = NoticeAlertFamily.Emergency) =>
        NoticeCategory.Create(
            NoticeCategoryId.New(),
            key,
            name,
            Now,
            defaultSeverity: NoticeSeverity.Critical,
            alertFamily: family).Value;

    private static IssuingAuthority Authority() =>
        IssuingAuthority.Create(
            IssuingAuthorityId.New(),
            "MU",
            "Passport and Immigration Office",
            AuthorityType.ImmigrationDepartment,
            Now,
            Administrator).Value;
}
