using Diaspora.Contracts.Trust.V1.Submission;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Trust.Domain;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Trust;

/// <summary>
/// The B3.0.2 submission contract, at the level a unit test can reach it.
/// <para>
/// Three things are under test: that the request model carries what a vertical needs without
/// naming a Trust type, that the subject is what decides whether two submissions are the same
/// claim, and that the aggregate records a subject without changing anything about how the claim
/// is judged. The service's own behaviour — correlation lookups, the submitter check — is
/// exercised end to end in the integration suite, where a real repository can answer.
/// </para>
/// </summary>
public sealed class TrustSubmissionContractTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    private static readonly UserId Member =
        UserId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    private static readonly Guid ProviderId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    // ── The published request model ──────────────────────────────────────

    [Fact]
    public void A_submission_defaults_to_the_member_as_its_subject()
    {
        // The default is what every submission meant before subjects existed. A vertical that
        // forgets to set one gets the old behaviour rather than an unroutable claim.
        var request = new TrustVerificationSubmissionRequest
        {
            VerificationType = "Identity",
            SubmittedBy = Member.Value
        };

        request.Subject.SubjectType.Should().Be(TrustVerificationSubjectTypes.Member);
        request.Subject.SubjectId.Should().BeNull();
    }

    [Fact]
    public void A_submission_carries_evidence_as_references_and_never_as_urls()
    {
        var mediaId = Guid.NewGuid();

        var request = new TrustVerificationSubmissionRequest
        {
            VerificationType = "Business",
            SubmittedBy = Member.Value,
            Evidence = [TrustVerificationEvidence.Media(mediaId)]
        };

        request.Evidence.Should().ContainSingle().Which.MediaId.Should().Be(mediaId);

        // The whole surface, asserted: there is nowhere on this contract to put a URL, which is
        // ADR-005's rule expressed as a type rather than as a convention somebody remembers.
        typeof(TrustVerificationSubmissionRequest)
            .GetProperties()
            .Should().NotContain(property => property.Name.Contains("Url", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_contract_names_no_trust_type()
    {
        // The property that makes the contract consumable. Every type reachable from the
        // request must live in Diaspora.Contracts or the BCL — the moment one of them is
        // Trust's, a consumer needs a project reference to the module that decides, and the
        // gap this phase closed reopens.
        var assemblies = typeof(TrustVerificationSubmissionRequest)
            .GetProperties()
            .Select(property => property.PropertyType)
            .Select(type => Nullable.GetUnderlyingType(type) ?? type)
            .Select(type => type.IsGenericType ? type.GetGenericArguments()[0] : type)
            .Select(type => type.Assembly.GetName().Name)
            .Distinct()
            .ToList();

        assemblies.Should().NotContain("Diaspora.Modules.Trust");
    }

    [Fact]
    public void A_subject_names_the_aggregate_a_vertical_owns()
    {
        var subject = TrustVerificationSubject.For(
            TrustVerificationSubjectTypes.Provider, ProviderId);

        subject.SubjectType.Should().Be("Provider");
        subject.SubjectId.Should().Be(ProviderId);
    }

    [Fact]
    public void A_vertical_can_name_a_subject_the_platform_has_never_heard_of()
    {
        // The reason SubjectType is a string. A Housing module verifying a tenancy must not
        // need a new member on an enum in a frozen assembly before it can submit anything.
        var subject = TrustVerificationSubject.For("Tenancy", ProviderId);

        subject.SubjectType.Should().Be("Tenancy");
    }

    // ── The aggregate ────────────────────────────────────────────────────

    [Fact]
    public void A_request_without_a_subject_is_exactly_what_b3_opened()
    {
        var request = VerificationRequest.Open(Member, VerificationType.Identity, Now);

        request.SubjectType.Should().BeNull();
        request.SubjectId.Should().BeNull();
        request.CorrelationId.Should().BeNull();
        request.RequestedBy.Should().BeNull();
        request.Status.Should().Be(VerificationStatus.Submitted);
    }

    [Fact]
    public void Recording_a_subject_changes_nothing_about_how_the_claim_is_judged()
    {
        var member = VerificationRequest.Open(Member, VerificationType.Business, Now);
        var subjected = VerificationRequest.Open(Member, VerificationType.Business, Now);

        subjected.WithSubject("Provider", ProviderId, null, "Providers");

        member.Queue(Now, TimeSpan.FromHours(48));
        subjected.Queue(Now, TimeSpan.FromHours(48));

        // Same status, same deadline, same workflow. The subject decides which claims are the
        // same claim; it has no vote on the outcome.
        subjected.Status.Should().Be(member.Status);
        subjected.ReviewDueAt.Should().Be(member.ReviewDueAt);
        subjected.Type.Should().Be(member.Type);
    }

    [Fact]
    public void A_subject_is_truncated_rather_than_allowed_to_overflow_its_column()
    {
        var request = VerificationRequest.Open(Member, VerificationType.Business, Now);

        request.WithSubject(new string('x', 500), ProviderId, null, new string('y', 500));

        request.SubjectType!.Length.Should().Be(VerificationRequest.MaxSubjectTypeLength);
        request.RequestedBy!.Length.Should().Be(VerificationRequest.MaxRequestedByLength);
    }

    // ── Evidence ─────────────────────────────────────────────────────────

    [Fact]
    public void Media_evidence_is_stored_as_a_reference_never_as_a_url()
    {
        var request = VerificationRequest.Open(Member, VerificationType.Business, Now);
        var mediaId = Guid.NewGuid();

        var document = request.AttachMediaEvidence(
            mediaId, VerificationDocumentType.BusinessRegistration, Now);

        document.MediaId.Should().Be(mediaId);
        document.DocumentType.Should().Be(VerificationDocumentType.BusinessRegistration);
        document.StorageKey.Should().Be($"media:{mediaId:D}");
        document.StorageKey.Should().NotContain("http");
    }

    [Fact]
    public void Several_pieces_of_media_evidence_all_survive()
    {
        // The bug this guards against is real and was found while writing it: AttachDocument
        // supersedes same-type documents, which is right for a member re-uploading "passport"
        // and catastrophic for a module submitting three photographs — two would vanish and a
        // reviewer would see a thinner claim than was made.
        var request = VerificationRequest.Open(Member, VerificationType.Business, Now);

        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();

        request.AttachMediaEvidence(first, VerificationDocumentType.BusinessRegistration, Now);
        request.AttachMediaEvidence(second, VerificationDocumentType.BusinessRegistration, Now);
        request.AttachMediaEvidence(third, VerificationDocumentType.BusinessRegistration, Now);

        request.Documents.Should().HaveCount(3);
        request.Documents.Select(document => document.MediaId)
            .Should().BeEquivalentTo([first, second, third]);
        request.Documents.Should().OnlyContain(
            document => document.Status == VerificationDocumentStatus.Pending);
    }

    [Fact]
    public void Attaching_the_same_media_twice_is_idempotent()
    {
        var request = VerificationRequest.Open(Member, VerificationType.Business, Now);
        var mediaId = Guid.NewGuid();

        request.AttachMediaEvidence(mediaId, VerificationDocumentType.BusinessRegistration, Now);
        request.AttachMediaEvidence(mediaId, VerificationDocumentType.BusinessRegistration, Now);

        request.Documents.Should().HaveCount(1);
    }

    [Fact]
    public void Evidence_cannot_be_added_to_a_decided_request()
    {
        // The rule that stops a member quietly improving a rejected submission after the fact,
        // asserted for the new entry point as well as the old one.
        var request = VerificationRequest.Open(Member, VerificationType.Business, Now);
        request.Queue(Now);
        request.AutoReject("rule", VerificationRejectionReason.INVALID_DOCUMENT, null, Now);

        var attach = () => request.AttachMediaEvidence(
            Guid.NewGuid(), VerificationDocumentType.BusinessRegistration, Now);

        attach.Should().Throw<DomainException>();
    }
}
