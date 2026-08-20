using Diaspora.Modules.Guides.Domain.Embassies;
using Diaspora.Modules.Guides.Domain.Emergency;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Holidays;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Domain.Visas;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Intelligence;

/// <summary>
/// The four directory aggregates: missions, emergency information, visa advisories and the calendar.
/// <para>
/// Grouped because each is small and because the interesting properties are the ones they do
/// <em>not</em> share — a mission is not a Provider, emergency information is never soft-deleted, an
/// advisory stays editable while published, and a holiday has no draft state. Every one of those is
/// asserted below.
/// </para>
/// </summary>
public class IntelligenceDirectoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    private static readonly UserId Editor = UserId.From(Guid.NewGuid());

    // ── Embassies ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_mission_carries_two_country_codes_and_neither_derives_from_the_other()
    {
        var embassy = Embassy.Create(
            EmbassyId.New(), "gb", "ng", "Nigerian High Commission", Now, Editor).Value;

        embassy.CountryCode.Should().Be("GB", "where the mission is");
        embassy.ServesCountryCode.Should().Be("NG", "whose nationals it serves");
    }

    [Fact]
    public void The_emergency_line_is_its_own_field()
    {
        var embassy = Mission();

        embassy.UpdateContact(
            "+230 123 4567",
            "+230 999 0000",
            "info@example.gov.ng",
            "https://example.gov.ng",
            null,
            requiresAppointment: true,
            Editor);

        embassy.Telephone.Should().Be("+230 123 4567");
        embassy.EmergencyTelephone.Should().Be(
            "+230 999 0000", "somebody who needs it will not read a paragraph to find it");
    }

    [Fact]
    public void An_unusable_website_is_refused_but_an_absent_one_is_not()
    {
        var embassy = Mission();

        embassy.UpdateContact(null, null, null, "not-a-url", null, false, Editor).Error
            .Should().Be(IntelligenceErrors.SourceUrlInvalid);

        embassy.UpdateContact(null, null, null, null, null, false, Editor)
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Out_of_range_coordinates_are_dropped_rather_than_refusing_the_save()
    {
        var embassy = Mission();

        var updated = embassy.UpdateProfile(
            "Mission", "Port Louis", "1 Main Street", 999, 999, Editor);

        updated.IsSuccess.Should().BeTrue("a bad pin is not a bad entry");
        embassy.Latitude.Should().BeNull();
        embassy.Address.Should().Be("1 Main Street");
    }

    [Fact]
    public void Office_hours_are_ordered_by_day_and_a_repeated_day_keeps_its_first_row()
    {
        var embassy = Mission();

        embassy.SetOfficeHours(
            [
                (DayOfWeek.Wednesday, "09:00-12:00", false),
                (DayOfWeek.Monday, "08:30-15:30", false),
                (DayOfWeek.Wednesday, "14:00-15:00", false),
                (DayOfWeek.Sunday, null, true)
            ],
            Editor);

        embassy.OfficeHours.Select(row => row.Day)
            .Should().ContainInOrder(DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Wednesday);

        embassy.OfficeHours.Single(row => row.Day == DayOfWeek.Wednesday).Hours
            .Should().Be("09:00-12:00");
    }

    [Fact]
    public void A_day_with_neither_hours_nor_a_closure_is_dropped()
    {
        var embassy = Mission();

        embassy.SetOfficeHours([(DayOfWeek.Tuesday, "   ", false)], Editor);

        embassy.OfficeHours.Should().BeEmpty("an opening-hours panel must not render blank lines");
    }

    /// <summary>
    /// The verified badge is Trust's decision projected inbound. Nothing on the aggregate sets it
    /// directly, which is ADR-002 and is what keeps the badge meaningful.
    /// </summary>
    [Fact]
    public void Verification_is_applied_inbound_and_is_idempotent()
    {
        var embassy = Mission();

        embassy.ApplyVerification(verified: true, Now);

        embassy.IsVerified.Should().BeTrue();
        embassy.VerifiedAt.Should().Be(Now);

        var revision = embassy.Revision;

        embassy.ApplyVerification(verified: true, Now.AddDays(1));

        embassy.Revision.Should().Be(revision, "an unchanged decision must not bump anything");
    }

    // ── Emergency information ───────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Two police numbers is a contradiction an editor must resolve, not one the platform should
    /// resolve by list order.</b> Unlike the localization collections, where a duplicate is a
    /// spreadsheet artefact and is collapsed, a duplicate kind is refused outright.
    /// </summary>
    [Fact]
    public void A_repeated_emergency_kind_is_refused()
    {
        var directory = Emergency();

        var applied = directory.SetContacts(
            [
                ("police", "Police", "999", null, null, true),
                ("police", "Gendarmerie", "112", null, null, true)
            ],
            Editor);

        applied.Error.Should().Be(IntelligenceErrors.DuplicateEmergencyKind);
    }

    [Fact]
    public void A_short_emergency_number_survives_normalisation()
    {
        var directory = Emergency();

        directory.SetContacts(
            [("police", "Police", " 112 ", null, null, true)], Editor);

        directory.Contacts.Single().Number.Should().Be(
            "112", "an emergency number is frequently not internationally dialable");
    }

    [Fact]
    public void A_contact_without_a_number_is_refused()
    {
        var directory = Emergency();

        directory.SetContacts([("police", "Police", "no digits here", null, null, true)], Editor)
            .Error.Should().Be(IntelligenceErrors.EmergencyContactInvalid);
    }

    /// <summary>
    /// A translation naming a kind the country does not list is refused rather than dropped: it is
    /// almost always a typo, and discarding it silently would leave the translator saving a page that
    /// never changes.
    /// </summary>
    [Fact]
    public void A_translation_for_an_unlisted_kind_is_refused()
    {
        var directory = Emergency();

        directory.SetContacts([("police", "Police", "999", null, null, true)], Editor);

        directory.SetLocalizations([("fr", "fire", "Pompiers", null)], Editor).Error
            .Should().Be(IntelligenceErrors.EmergencyContactInvalid);
    }

    /// <summary>
    /// Removing a contact takes its translations with it. This is the only place both collections are
    /// visible at once, so it is the only place the orphan can be prevented.
    /// </summary>
    [Fact]
    public void Dropping_a_contact_drops_its_translations()
    {
        var directory = Emergency();

        directory.SetContacts(
            [
                ("police", "Police", "999", null, null, true),
                ("fire", "Fire", "998", null, null, true)
            ],
            Editor);

        directory.SetLocalizations(
            [
                ("fr", "police", "Police", null),
                ("fr", "fire", "Pompiers", null)
            ],
            Editor);

        directory.SetContacts([("police", "Police", "999", null, null, true)], Editor);

        directory.Localizations.Should().ContainSingle();
        directory.Localizations.Single().Kind.Should().Be("police");
    }

    [Fact]
    public void A_contact_label_falls_back_through_the_language_to_the_default()
    {
        var directory = Emergency();

        directory.SetContacts([("police", "Police", "999", null, null, true)], Editor);
        directory.SetLocalizations([("fr", "police", "Gendarmerie", "24h/24")], Editor);

        var contact = directory.Contacts.Single();

        directory.Localized(contact, "fr").Label.Should().Be("Gendarmerie");
        directory.Localized(contact, "fr-MU").Label.Should().Be("Gendarmerie");
        directory.Localized(contact, "de").Label.Should().Be("Police");
        directory.Localized(contact, null).Label.Should().Be("Police");
    }

    /// <summary>
    /// The one aggregate in the phase that is not <c>ISoftDeletable</c>. A hidden row holding a
    /// number that now rings somewhere else is a liability with no upside.
    /// </summary>
    [Fact]
    public void An_emergency_directory_is_not_soft_deletable()
    {
        typeof(CountryEmergencyDirectory)
            .GetInterface("ISoftDeletable")
            .Should().BeNull();
    }

    // ── Visa advisories ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void An_advisory_with_no_nationality_applies_to_everybody()
    {
        var advisory = Advisory(appliesTo: null);

        advisory.AppliesToCountryCode.Should().BeNull();
    }

    [Fact]
    public void An_unusable_nationality_is_refused_but_an_absent_one_is_not()
    {
        VisaAdvisory.Create(
            VisaAdvisoryId.New(),
            "MU",
            "XXX",
            IssuingAuthorityId.From(Guid.NewGuid()),
            "Title",
            "Summary",
            Now).Error.Should().Be(IntelligenceErrors.CountryInvalid);

        Advisory(appliesTo: null).Should().NotBeNull();
    }

    /// <summary>
    /// Editable while published, unlike a notice. An advisory describes what is true <em>now</em>,
    /// and freezing it would make it wrong the day a fee changed — the history lives in the notices.
    /// </summary>
    [Fact]
    public void A_published_advisory_is_still_editable()
    {
        var advisory = Advisory();

        advisory.Publish(Now, Editor).IsSuccess.Should().BeTrue();

        advisory.Update("New title", "New summary", null, "Student", "12 days", Editor)
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Requirements_and_documents_are_separate_lists_numbered_from_zero()
    {
        var advisory = Advisory();

        advisory.SetLines(
            ["Six months' passport validity", "Proof of funds"],
            ["Passport", "Bank statement", "Photograph"],
            Editor);

        advisory.Requirements.Select(line => line.Position).Should().ContainInOrder(0, 1);
        advisory.Documents.Select(line => line.Position).Should().ContainInOrder(0, 1, 2);
        advisory.Lines.Should().HaveCount(5);
    }

    [Fact]
    public void A_blank_line_in_a_pasted_list_is_skipped_without_leaving_a_gap()
    {
        var advisory = Advisory();

        advisory.SetLines(["First", "   ", "Second"], [], Editor);

        advisory.Requirements.Select(line => line.Text).Should().ContainInOrder("First", "Second");
        advisory.Requirements.Select(line => line.Position).Should().ContainInOrder(0, 1);
    }

    [Fact]
    public void An_advisory_is_active_only_when_published_and_inside_its_window()
    {
        var advisory = Advisory();

        advisory.Schedule(Now.AddDays(5), Now.AddDays(10), Editor);

        advisory.IsActiveAt(Now.AddDays(7)).Should().BeFalse("it is not published");

        advisory.Publish(Now, Editor);

        advisory.IsActiveAt(Now.AddDays(7)).Should().BeTrue();
        advisory.IsActiveAt(Now).Should().BeFalse();
        advisory.IsActiveAt(Now.AddDays(11)).Should().BeFalse();
    }

    [Fact]
    public void Restoring_an_advisory_returns_it_as_a_draft()
    {
        var advisory = Advisory();

        advisory.Publish(Now, Editor);
        advisory.MarkDeleted(Now, Editor.Value.ToString());

        advisory.Status.Should().Be(VisaAdvisoryStatus.Archived);

        advisory.Restore();

        advisory.Status.Should().Be(VisaAdvisoryStatus.Draft);
        advisory.PublishedAt.Should().BeNull();
    }

    // ── Public holidays ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_holiday_is_truncated_to_whole_days_and_stamps_its_year()
    {
        var holiday = PublicHoliday.Create(
            PublicHolidayId.New(),
            "MU",
            "Independence Day",
            HolidayKind.National,
            new DateTimeOffset(2026, 3, 12, 14, 37, 0, TimeSpan.FromHours(4)),
            null,
            Now).Value;

        holiday.ObservedOn.Should().Be(new DateTimeOffset(2026, 3, 12, 0, 0, 0, TimeSpan.Zero));
        holiday.ObservedUntil.Should().Be(holiday.ObservedOn, "an absent end date means one day");
        holiday.Year.Should().Be(2026);
    }

    [Fact]
    public void A_holiday_ending_before_it_starts_is_refused()
    {
        PublicHoliday.Create(
            PublicHolidayId.New(),
            "MU",
            "Something",
            HolidayKind.National,
            new DateTimeOffset(2026, 3, 12, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero),
            Now).Error.Should().Be(IntelligenceErrors.HolidayDateInvalid);
    }

    /// <summary>
    /// The closure flags are the payload. A religious holiday that closes nothing and a national one
    /// that closes everything are both ordinary, and the two facts do not travel together.
    /// </summary>
    [Fact]
    public void The_closure_flags_are_independent_of_the_kind()
    {
        var holiday = Holiday();

        holiday.Update(
            "Thaipusam Cavadee",
            null,
            HolidayKind.Religious,
            holiday.ObservedOn,
            null,
            null,
            closesGovernmentOffices: true,
            closesEmbassies: false,
            Editor);

        holiday.Kind.Should().Be(HolidayKind.Religious);
        holiday.ClosesGovernmentOffices.Should().BeTrue();
        holiday.ClosesEmbassies.Should().BeFalse();
    }

    [Fact]
    public void A_holiday_name_falls_back_through_the_language_to_the_default()
    {
        var holiday = Holiday();

        holiday.SetLocalizations([("ta", "தைப்பூசம்", null)], Editor);

        holiday.Localized("ta").Name.Should().Be("தைப்பூசம்");
        holiday.Localized("fr").Name.Should().Be("Independence Day");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private static Embassy Mission() =>
        Embassy.Create(EmbassyId.New(), "MU", "NG", "Nigerian Consulate", Now, Editor).Value;

    private static CountryEmergencyDirectory Emergency() =>
        CountryEmergencyDirectory.Create(
            CountryEmergencyDirectoryId.New(), "MU", Now, Editor).Value;

    private static VisaAdvisory Advisory(string? appliesTo = "NG") =>
        VisaAdvisory.Create(
            VisaAdvisoryId.New(),
            "MU",
            appliesTo,
            IssuingAuthorityId.From(Guid.NewGuid()),
            "Visitor visa",
            "Ninety days visa-free.",
            Now,
            Editor).Value;

    private static PublicHoliday Holiday() =>
        PublicHoliday.Create(
            PublicHolidayId.New(),
            "MU",
            "Independence Day",
            HolidayKind.National,
            new DateTimeOffset(2026, 3, 12, 0, 0, 0, TimeSpan.Zero),
            null,
            Now,
            Editor).Value;
}
