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
/// The official notice lifecycle.
/// <para>
/// <b>The rules here are the difference between a guide and a government statement.</b> A guide is
/// somebody's experience and improves by being edited; a notice is a claim about what an institution
/// announced, and correcting it after publication would rewrite history members have already acted
/// on. Every assertion in this file is one of the consequences of that.
/// </para>
/// </summary>
public class OfficialNoticeTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    private static readonly UserId Editor = UserId.From(Guid.NewGuid());

    private static readonly IssuingAuthorityId Ministry =
        IssuingAuthorityId.From(Guid.NewGuid());

    // ── Creation ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_created_notice_is_a_draft_and_is_unverified()
    {
        var notice = Create();

        notice.Status.Should().Be(NoticeStatus.Draft);
        notice.VerificationState.Should().Be(NoticeVerificationState.Unverified);
        notice.CountryCode.Should().Be("MU");
        notice.IsReaderVisible.Should().BeFalse();
    }

    [Fact]
    public void Creation_normalises_the_country_and_the_category_key()
    {
        var created = OfficialNotice.Create(
            OfficialNoticeId.New(),
            "  mu  ",
            "  Visa-Advisories  ",
            Ministry,
            "New visa rules",
            "The rules change on 1 April.",
            "Full text of the circular.",
            NoticeSeverity.High,
            Now);

        created.IsSuccess.Should().BeTrue();
        created.Value.CountryCode.Should().Be("MU");
        created.Value.CategoryKey.Should().Be("visa-advisories");
    }

    [Theory]
    [InlineData("XX1")]
    [InlineData("")]
    [InlineData("   ")]
    public void An_unusable_country_code_is_refused(string countryCode)
    {
        var created = OfficialNotice.Create(
            OfficialNoticeId.New(),
            countryCode,
            "visa-advisories",
            Ministry,
            "Title",
            "Summary",
            "Content",
            NoticeSeverity.High,
            Now);

        created.IsFailure.Should().BeTrue();
        created.Error.Should().Be(IntelligenceErrors.CountryInvalid);
    }

    /// <summary>
    /// <b>The severity rank is the fix for a defect the enum's storage would otherwise cause.</b>
    /// Severity is stored as text so a table is legible; ordering by that text puts Informational
    /// above Critical. The rank is written in the same place the enum is, so they cannot drift.
    /// </summary>
    [Theory]
    [InlineData(NoticeSeverity.Informational, 0)]
    [InlineData(NoticeSeverity.Low, 1)]
    [InlineData(NoticeSeverity.Medium, 2)]
    [InlineData(NoticeSeverity.High, 3)]
    [InlineData(NoticeSeverity.Critical, 4)]
    public void The_severity_rank_tracks_the_severity(NoticeSeverity severity, int rank)
    {
        var notice = Create(severity);

        notice.SeverityRank.Should().Be(rank);

        notice.Reclassify("emergency-alerts", NoticeSeverity.Critical, 0, Editor);

        notice.SeverityRank.Should().Be(4);
        notice.Severity.Should().Be(NoticeSeverity.Critical);
    }

    // ── The publication path ────────────────────────────────────────────────────────────────

    [Fact]
    public void A_notice_moves_draft_to_submitted_to_verified_to_published()
    {
        var notice = Create();

        notice.Submit(Editor).IsSuccess.Should().BeTrue();
        notice.Status.Should().Be(NoticeStatus.Submitted);
        notice.VerificationState.Should().Be(NoticeVerificationState.Pending);

        notice.MarkVerified(Now, Editor).IsSuccess.Should().BeTrue();
        notice.Status.Should().Be(NoticeStatus.Verified);
        notice.VerificationState.Should().Be(NoticeVerificationState.Verified);

        notice.Publish(Now, Editor).IsSuccess.Should().BeTrue();
        notice.Status.Should().Be(NoticeStatus.Published);
        notice.IsReaderVisible.Should().BeTrue();
    }

    [Fact]
    public void An_unverified_notice_cannot_be_published()
    {
        var notice = Create();

        var published = notice.Publish(Now, Editor);

        published.IsFailure.Should().BeTrue();
        published.Error.Should().Be(IntelligenceErrors.NoticeNotPublishable);
    }

    [Fact]
    public void Only_a_submitted_notice_can_be_verified()
    {
        var notice = Create();

        notice.MarkVerified(Now, Editor).Error
            .Should().Be(IntelligenceErrors.NoticeNotVerifiable);
    }

    /// <summary>
    /// A refusal returns the notice to draft rather than to a terminal state: the ordinary cause is
    /// a missing reference number, and making the editor start again would lose the body they had
    /// already transcribed.
    /// </summary>
    [Fact]
    public void A_refused_verification_returns_the_notice_to_draft_and_records_why()
    {
        var notice = Create();

        notice.Submit(Editor);
        notice.MarkRejected(Now, Editor, "No gazette number supplied.");

        notice.Status.Should().Be(NoticeStatus.Draft);
        notice.VerificationState.Should().Be(NoticeVerificationState.Rejected);
        notice.VerificationNote.Should().Be("No gazette number supplied.");
    }

    /// <summary>
    /// <b>The rule that stops the whole workflow being theatre.</b> A verifier confirmed a specific
    /// wording against a specific source; the next wording is a different claim. Keeping the
    /// verification would let an editor put anything at all under a confirmed badge by verifying
    /// something innocuous first.
    /// </summary>
    [Fact]
    public void Editing_a_verified_notice_returns_it_to_draft_and_drops_the_verification()
    {
        var notice = Create();

        notice.Submit(Editor);
        notice.MarkVerified(Now, Editor);

        notice.UpdateContent("Something else entirely", "New summary", "New body", Editor)
            .IsSuccess.Should().BeTrue();

        notice.Status.Should().Be(NoticeStatus.Draft);
        notice.VerificationState.Should().Be(NoticeVerificationState.Unverified);
        notice.VerifiedAt.Should().BeNull();
    }

    [Fact]
    public void Reassigning_the_authority_also_drops_the_verification()
    {
        var notice = Create();

        notice.Submit(Editor);
        notice.MarkVerified(Now, Editor);

        notice.Reassign(IssuingAuthorityId.From(Guid.NewGuid()), Editor);

        notice.Status.Should().Be(NoticeStatus.Draft);
        notice.VerificationState.Should().Be(NoticeVerificationState.Unverified);
    }

    [Fact]
    public void A_published_notice_cannot_have_its_text_rewritten()
    {
        var notice = Published();

        notice.UpdateContent("Rewritten", "Rewritten", "Rewritten", Editor).Error
            .Should().Be(IntelligenceErrors.NoticeNotEditable);
    }

    /// <summary>
    /// Presentation stays editable while published, and the translation case is what justifies it: a
    /// community translator finishing a Kreol version of a live health advisory three days after it
    /// went out is the system working.
    /// </summary>
    [Fact]
    public void A_published_notice_still_accepts_translations_attachments_and_links()
    {
        var notice = Published();

        notice.SetLocalizations(
            [("fr", "Nouvelles règles", "Les règles changent.", "Texte complet.")], Editor)
            .IsSuccess.Should().BeTrue();

        notice.SetAttachments([(Guid.NewGuid(), "The circular")], Editor)
            .IsSuccess.Should().BeTrue();

        notice.SetReferences(
            [(NoticeReferenceKind.Guide, Guid.NewGuid(), "How to apply")], Editor)
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void A_submitted_notice_is_frozen_even_for_presentation()
    {
        var notice = Create();

        notice.Submit(Editor);

        notice.SetLocalizations([("fr", "T", "S", "C")], Editor).Error
            .Should().Be(IntelligenceErrors.NoticeNotEditable);
    }

    // ── Supersession ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Superseding_names_the_replacement_and_keeps_the_notice_readable()
    {
        var notice = Published();
        var replacement = OfficialNoticeId.New();

        notice.Supersede(replacement, Now, Editor).IsSuccess.Should().BeTrue();

        notice.Status.Should().Be(NoticeStatus.Superseded);
        notice.SupersededById.Should().Be(replacement);
        notice.IsReaderVisible.Should().BeTrue("somebody who followed the old rule needs to see it");
    }

    [Fact]
    public void A_notice_cannot_supersede_itself()
    {
        var notice = Published();

        notice.Supersede(notice.Id, Now, Editor).Error
            .Should().Be(IntelligenceErrors.SupersedeSelf);
    }

    [Fact]
    public void An_unpublished_notice_cannot_be_superseded()
    {
        var notice = Create();

        notice.Supersede(OfficialNoticeId.New(), Now, Editor).Error
            .Should().Be(IntelligenceErrors.SupersedeNotPublished);
    }

    // ── Dates and expiry ────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_publication_date_in_the_future_is_refused()
    {
        var notice = Create();

        notice.Schedule(Now.AddDays(1), null, null, Now, Editor).Error
            .Should().Be(IntelligenceErrors.PublicationInFuture);
    }

    [Fact]
    public void An_expiry_before_the_effective_date_is_refused()
    {
        var notice = Create();

        notice.Schedule(null, Now.AddDays(10), Now.AddDays(5), Now, Editor).Error
            .Should().Be(IntelligenceErrors.EffectiveAfterExpiry);
    }

    [Fact]
    public void A_notice_is_active_only_inside_its_window()
    {
        var notice = Create();

        notice.Schedule(null, Now.AddDays(5), Now.AddDays(10), Now, Editor);
        notice.Submit(Editor);
        notice.MarkVerified(Now, Editor);
        notice.Publish(Now, Editor);

        notice.IsActiveAt(Now).Should().BeFalse("it has not taken effect yet");
        notice.IsActiveAt(Now.AddDays(7)).Should().BeTrue();
        notice.IsActiveAt(Now.AddDays(11)).Should().BeFalse("it has expired");
    }

    /// <summary>
    /// <b>The sweep must be idempotent, and this is the property that makes it so.</b> Without the
    /// marker it would re-announce every expired notice on every pass — bumping a revision an editor
    /// is holding and filling the outbox with the same message forever.
    /// </summary>
    [Fact]
    public void Expiring_is_announced_once_and_leaves_the_notice_readable()
    {
        var notice = PublishedExpiringOn(Now.AddDays(1));

        var after = Now.AddDays(2);

        notice.Expire(after).Should().BeTrue();
        notice.Expire(after).Should().BeFalse("the second pass must do nothing");

        notice.Status.Should().Be(NoticeStatus.Published, "an expired notice stays readable");
        notice.IsActiveAt(after).Should().BeFalse();
        notice.ExpiredAt.Should().Be(after);
    }

    /// <summary>
    /// <b>Dates are frozen at publication, exactly like the text, and the reason is the same.</b>
    /// "Effective 1 March until 30 June" is part of what the authority said. A government extending
    /// the deadline has announced something new, and the platform records that as a new notice that
    /// supersedes this one — not as a silent edit to a document members have already read.
    /// </summary>
    [Fact]
    public void A_published_notice_cannot_have_its_dates_rewritten()
    {
        var notice = PublishedExpiringOn(Now.AddDays(1));

        notice.Schedule(null, null, Now.AddDays(30), Now, Editor).Error
            .Should().Be(IntelligenceErrors.NoticeNotEditable);
    }

    /// <summary>
    /// The reachable path by which the expiry marker is cleared: withdraw, restore, reschedule,
    /// republish. Worth asserting because a stale marker would make the second expiry silent — the
    /// sweep would see a notice already announced and say nothing.
    /// </summary>
    [Fact]
    public void Restoring_and_rescheduling_lets_the_sweep_announce_the_next_expiry()
    {
        var notice = PublishedExpiringOn(Now.AddDays(1));

        notice.Expire(Now.AddDays(2)).Should().BeTrue();

        notice.MarkDeleted(Now.AddDays(2), Editor.Value.ToString());
        notice.Restore();

        notice.ExpiredAt.Should().BeNull("a restored notice has not been announced as expired");

        notice.Schedule(null, null, Now.AddDays(30), Now.AddDays(2), Editor)
            .IsSuccess.Should().BeTrue("a restored notice is a draft again");

        notice.Submit(Editor);
        notice.MarkVerified(Now.AddDays(2), Editor);
        notice.Publish(Now.AddDays(2), Editor);

        notice.Expire(Now.AddDays(31)).Should().BeTrue();
    }

    // ── Collections ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_repeated_reference_is_refused_rather_than_collapsed()
    {
        var notice = Create();
        var guideId = Guid.NewGuid();

        var applied = notice.SetReferences(
            [
                (NoticeReferenceKind.Guide, guideId, "First"),
                (NoticeReferenceKind.Guide, guideId, "Second")
            ],
            Editor);

        applied.Error.Should().Be(IntelligenceErrors.DuplicateReference);
    }

    [Fact]
    public void The_same_target_under_two_kinds_is_allowed()
    {
        var notice = Create();
        var targetId = Guid.NewGuid();

        notice.SetReferences(
            [
                (NoticeReferenceKind.Guide, targetId, null),
                (NoticeReferenceKind.Event, targetId, null)
            ],
            Editor)
            .IsSuccess.Should().BeTrue();

        notice.References.Should().HaveCount(2);
    }

    /// <summary>
    /// A partial translation is refused. A translated heading over an untranslated instruction is
    /// worse than no translation: a reader picks it and then cannot read the part that matters.
    /// </summary>
    [Fact]
    public void A_translation_missing_any_of_its_three_parts_is_refused()
    {
        var notice = Create();

        notice.SetLocalizations([("fr", "Titre", "Résumé", null)], Editor).Error
            .Should().Be(IntelligenceErrors.LocaleInvalid);
    }

    [Fact]
    public void A_repeated_locale_keeps_its_first_entry()
    {
        var notice = Create();

        notice.SetLocalizations(
            [
                ("fr", "First", "S", "C"),
                ("fr", "Second", "S", "C")
            ],
            Editor);

        notice.Localizations.Should().ContainSingle();
        notice.Localizations[0].Title.Should().Be("First");
    }

    [Fact]
    public void Localized_falls_back_exact_then_language_then_default()
    {
        var notice = Create();

        notice.SetLocalizations([("fr", "Titre", "Résumé", "Texte")], Editor);

        notice.Localized("fr").Title.Should().Be("Titre");
        notice.Localized("fr-MU").Title.Should().Be("Titre", "the language half matches");
        notice.Localized("de").Title.Should().Be("New visa rules", "nothing matches");
        notice.Localized(null).Title.Should().Be("New visa rules");
    }

    // ── Sourcing ────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,x")]
    [InlineData("/relative/path")]
    [InlineData("not a url at all")]
    public void An_unusable_source_url_is_refused(string url)
    {
        var notice = Create();

        notice.AttributeSource("MHA/2026/14", url, Editor).Error
            .Should().Be(IntelligenceErrors.SourceUrlInvalid);
    }

    [Fact]
    public void A_reference_number_keeps_its_case()
    {
        var notice = Create();

        notice.AttributeSource("MHA/IMM/2026/014", null, Editor);

        notice.ReferenceNumber.Should().Be(
            "MHA/IMM/2026/014", "a clerk will not find a lower-cased file number");
    }

    // ── Engagement and concurrency ──────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Engagement is not an edit.</b> A notice read by ten thousand people during a border
    /// closure must not give the editor trying to update it ten thousand concurrency conflicts, nor
    /// fill the outbox with ten thousand messages.
    /// </summary>
    [Fact]
    public void Counters_bump_no_revision_and_raise_no_event()
    {
        var notice = Published();

        var revision = notice.Revision;

        notice.ClearDomainEvents();

        notice.RecordView();
        notice.RecordClickThrough();
        notice.RecordCompletion();

        notice.ViewCount.Should().Be(1);
        notice.ClickThroughCount.Should().Be(1);
        notice.CompletionCount.Should().Be(1);
        notice.Revision.Should().Be(revision);
        notice.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void A_stale_revision_is_refused()
    {
        var notice = Create();

        notice.UpdateContent("New", "New", "New", Editor);

        notice.EnsureRevision(0).Error.Should().Be(IntelligenceErrors.RevisionConflict);
        notice.EnsureRevision(notice.Revision).IsSuccess.Should().BeTrue();
        notice.EnsureRevision(null).IsSuccess.Should().BeTrue("absent skips the check");
    }

    // ── Archival ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Restoring brings a notice back as a draft, never as published. Republishing a withdrawn
    /// official statement without anybody confirming it is still true is the one outcome nobody
    /// wants.
    /// </summary>
    [Fact]
    public void Restoring_returns_a_notice_as_an_unverified_draft()
    {
        var notice = Published();

        notice.MarkDeleted(Now, Editor.Value.ToString());

        notice.IsDeleted.Should().BeTrue();
        notice.Status.Should().Be(NoticeStatus.Archived);

        notice.Restore();

        notice.IsDeleted.Should().BeFalse();
        notice.Status.Should().Be(NoticeStatus.Draft);
        notice.PublishedAt.Should().BeNull();
        notice.VerificationState.Should().Be(NoticeVerificationState.Unverified);
    }

    // ── Events ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The summary travels on the event, and B13.1 is why.</b> Every publication contract
    /// requires one; a relay that had none would write a placeholder, the messages would go out, the
    /// consumers would parse them, and every one would be empty — silently, for a whole phase.
    /// </summary>
    [Fact]
    public void The_change_event_carries_everything_the_relay_publishes()
    {
        var notice = Create();

        notice.ClearDomainEvents();
        notice.Submit(Editor);
        notice.MarkVerified(Now, Editor);
        notice.ClearDomainEvents();
        notice.Publish(Now, Editor);

        var raised = notice.DomainEvents
            .OfType<OfficialNoticeChangedDomainEvent>()
            .Single();

        raised.Change.Should().Be(IntelligenceChangeKinds.Published);
        raised.CountryCode.Should().Be("MU");
        raised.CategoryKey.Should().Be("visa-advisories");
        raised.Title.Should().Be("New visa rules");
        raised.Summary.Should().Be("The rules change on 1 April.");
        raised.Severity.Should().Be(NoticeSeverity.High);
        raised.AuthorityId.Should().Be(Ministry);
        raised.ChangedBy.Should().Be(Editor);
    }

    [Fact]
    public void A_supersede_event_names_the_replacement()
    {
        var notice = Published();
        var replacement = OfficialNoticeId.New();

        notice.ClearDomainEvents();
        notice.Supersede(replacement, Now, Editor);

        notice.DomainEvents
            .OfType<OfficialNoticeChangedDomainEvent>()
            .Single()
            .SupersededBy
            .Should().Be(replacement);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private static OfficialNotice Create(NoticeSeverity severity = NoticeSeverity.High) =>
        OfficialNotice.Create(
            OfficialNoticeId.New(),
            "MU",
            "visa-advisories",
            Ministry,
            "New visa rules",
            "The rules change on 1 April.",
            "Full text of the circular.",
            severity,
            Now,
            Editor).Value;

    private static OfficialNotice Published()
    {
        var notice = Create();

        notice.Submit(Editor);
        notice.MarkVerified(Now, Editor);
        notice.Publish(Now, Editor);

        return notice;
    }

    /// <summary>
    /// A published notice with an expiry, scheduled <em>before</em> publication — which is the only
    /// order the aggregate allows, because dates freeze along with the text.
    /// </summary>
    private static OfficialNotice PublishedExpiringOn(DateTimeOffset expiry)
    {
        var notice = Create();

        notice.Schedule(null, null, expiry, Now, Editor);
        notice.Submit(Editor);
        notice.MarkVerified(Now, Editor);
        notice.Publish(Now, Editor);

        return notice;
    }
}
