using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Trust.V1.Submission;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The B3.0.2 submission contract, against the real container and a real database.
/// <para>
/// These are the tests that could not be written before the phase: every one of them resolves
/// <see cref="ITrustVerificationSubmission"/> from the composed host, which is the thing that did
/// not exist. What they assert is that a case really is opened, that the identifier returned is
/// the one Trust holds — the property B7's outcome handlers depend on and the stub could never
/// satisfy — and that repeating a submission does not open a second.
/// </para>
/// </summary>
public sealed class TrustSubmissionContractTests : IdentityTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public TrustSubmissionContractTests(CustomWebApplicationFactory factory) : base(factory) { }

    /// <summary>
    /// A registration document, which is what Trust's <c>Business</c> handler accepts. Evidence
    /// typed <c>Other</c> would be refused — that rule is Trust's and predates this contract.
    /// </summary>
    private static TrustVerificationEvidence BusinessEvidence() =>
        TrustVerificationEvidence.Document(Guid.NewGuid(), "BusinessRegistration");

    private async Task<Guid> RegisteredMemberAsync()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        return auth.User.Id;
    }

    private async Task<T> WithSubmissionAsync<T>(Func<ITrustVerificationSubmission, Task<T>> work)
    {
        using var scope = Factory.Services.CreateScope();
        var submission = scope.ServiceProvider.GetRequiredService<ITrustVerificationSubmission>();
        return await work(submission);
    }

    private static TrustVerificationSubmissionRequest BusinessClaim(
        Guid member,
        Guid? providerId = null,
        Guid? correlationId = null,
        IReadOnlyList<TrustVerificationEvidence>? evidence = null) =>
        new()
        {
            VerificationType = "Business",
            SubmittedBy = member,
            Subject = providerId is { } id
                ? TrustVerificationSubject.For("Provider", id)
                : TrustVerificationSubject.Member,
            Evidence = evidence ?? [BusinessEvidence()],
            CorrelationId = correlationId,
            RequestedBy = "Providers"
        };

    // ── The contract composes ────────────────────────────────────────────

    [Fact]
    public void The_submission_contract_resolves_from_the_composed_host()
    {
        // Finding 7, inverted. Before B3.0.2 nothing answered this resolution, which is exactly
        // why B7 returned a stub.
        using var scope = Factory.Services.CreateScope();

        var submission = scope.ServiceProvider.GetService<ITrustVerificationSubmission>();

        submission.Should().NotBeNull("the Trust module registers the contract it publishes");
    }

    // ── Submission opens a real case ─────────────────────────────────────

    [Fact]
    public async Task A_submission_opens_a_verification_the_module_actually_holds()
    {
        var member = await RegisteredMemberAsync();
        var providerId = Guid.NewGuid();

        var result = await WithSubmissionAsync(submission =>
            submission.SubmitAsync(BusinessClaim(member, providerId)));

        result.IsSuccess.Should().BeTrue();
        result.Value.IsDuplicate.Should().BeFalse();
        result.Value.VerificationId.Should().NotBeEmpty();

        // The identifier is Trust's own, not a number the caller invented. This is the single
        // assertion that distinguishes the contract from the stub it replaced: an outcome event
        // quotes this value, and B7 matches its claim on it.
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var stored = await context.Set<VerificationRequest>()
            .FirstOrDefaultAsync(request =>
                request.Id == VerificationRequestId.From(result.Value.VerificationId));

        stored.Should().NotBeNull();
        stored!.UserId.Should().Be(UserId.From(member));
        stored.Type.Should().Be(VerificationType.Business);
        stored.SubjectType.Should().Be("Provider");
        stored.SubjectId.Should().Be(providerId);
        stored.RequestedBy.Should().Be("Providers");
    }

    [Fact]
    public async Task A_submission_enters_the_existing_review_workflow()
    {
        var member = await RegisteredMemberAsync();

        var result = await WithSubmissionAsync(submission =>
            submission.SubmitAsync(BusinessClaim(member, Guid.NewGuid())));

        // Queued for a human, exactly as a member's own submission is. The contract feeds the
        // pipeline that already existed rather than a parallel one.
        result.Value.Status.Should().Be(nameof(VerificationStatus.Pending));
        result.Value.SubmittedAt.Should().NotBe(default);
        result.Value.ReviewDueAt.Should().NotBeNull("the Business type sets a five-day SLA");
    }

    [Fact]
    public async Task Evidence_is_stored_as_a_media_reference()
    {
        var member = await RegisteredMemberAsync();
        var first = BusinessEvidence();
        var second = BusinessEvidence();

        var result = await WithSubmissionAsync(submission => submission.SubmitAsync(
            BusinessClaim(member, Guid.NewGuid(), evidence: [first, second])));

        result.IsSuccess.Should().BeTrue();

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var documents = await context.Set<VerificationDocument>()
            .Where(document =>
                document.RequestId == VerificationRequestId.From(result.Value.VerificationId))
            .ToListAsync();

        documents.Should().HaveCount(2, "both pieces of evidence survive, neither supersedes");
        documents.Select(document => document.MediaId)
            .Should().BeEquivalentTo([first.MediaId, second.MediaId]);

        // References, never URLs. ADR-005 asserted against the stored row.
        documents.Should().OnlyContain(document => document.StorageKey.StartsWith("media:"));
        documents.Should().NotContain(document => document.StorageKey.Contains("http"));
        documents.Should().OnlyContain(
            document => document.DocumentType == VerificationDocumentType.BusinessRegistration);
    }

    [Fact]
    public async Task Evidence_a_type_does_not_accept_is_refused_rather_than_queued()
    {
        // Trust's existing rule, reached through the new route. A reviewer should never be
        // handed a business claim whose only evidence is an unlabelled file.
        var member = await RegisteredMemberAsync();

        var result = await WithSubmissionAsync(submission => submission.SubmitAsync(
            BusinessClaim(
                member,
                Guid.NewGuid(),
                evidence: [TrustVerificationEvidence.Media(Guid.NewGuid())])));

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task A_document_type_trust_does_not_recognise_is_refused()
    {
        var member = await RegisteredMemberAsync();

        var result = await WithSubmissionAsync(submission => submission.SubmitAsync(
            BusinessClaim(
                member,
                Guid.NewGuid(),
                evidence: [TrustVerificationEvidence.Document(Guid.NewGuid(), "NotADocument")])));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("trust.document.type-unsupported");
    }

    // ── Idempotency ──────────────────────────────────────────────────────

    [Fact]
    public async Task A_repeat_submission_for_the_same_subject_returns_the_original_case()
    {
        var member = await RegisteredMemberAsync();
        var providerId = Guid.NewGuid();

        var first = await WithSubmissionAsync(submission =>
            submission.SubmitAsync(BusinessClaim(member, providerId)));

        var second = await WithSubmissionAsync(submission =>
            submission.SubmitAsync(BusinessClaim(member, providerId)));

        second.IsSuccess.Should().BeTrue("a retry is an answer, not an error");
        second.Value.IsDuplicate.Should().BeTrue();
        second.Value.VerificationId.Should().Be(first.Value.VerificationId);
    }

    [Fact]
    public async Task A_correlation_identifier_makes_a_retry_free()
    {
        var member = await RegisteredMemberAsync();
        var correlation = Guid.NewGuid();
        var providerId = Guid.NewGuid();

        var first = await WithSubmissionAsync(submission =>
            submission.SubmitAsync(BusinessClaim(member, providerId, correlation)));

        var second = await WithSubmissionAsync(submission =>
            submission.SubmitAsync(BusinessClaim(member, providerId, correlation)));

        first.IsSuccess.Should().BeTrue();
        second.Value.IsDuplicate.Should().BeTrue();
        second.Value.VerificationId.Should().Be(first.Value.VerificationId);

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var matching = await context.Set<VerificationRequest>()
            .CountAsync(candidate => candidate.CorrelationId == correlation);

        matching.Should().Be(1, "one claim, one case, however many times it was sent");
    }

    [Fact]
    public async Task Two_subjects_of_the_same_type_are_two_claims()
    {
        // The case the member-scoped duplicate rule got wrong and the reason subjects exist: one
        // member, two businesses, two genuinely different verifications.
        var member = await RegisteredMemberAsync();

        var first = await WithSubmissionAsync(submission =>
            submission.SubmitAsync(BusinessClaim(member, Guid.NewGuid())));

        var second = await WithSubmissionAsync(submission =>
            submission.SubmitAsync(BusinessClaim(member, Guid.NewGuid())));

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        second.Value.IsDuplicate.Should().BeFalse();
        second.Value.VerificationId.Should().NotBe(first.Value.VerificationId);
    }

    [Fact]
    public async Task A_subject_claim_does_not_block_the_members_own_verification()
    {
        // The other half of the same rule. A member who has asked for their business to be
        // verified has still not asked for themselves to be, and must not be refused.
        var member = await RegisteredMemberAsync();

        var business = await WithSubmissionAsync(submission =>
            submission.SubmitAsync(BusinessClaim(member, Guid.NewGuid())));

        var personal = await WithSubmissionAsync(submission =>
            submission.SubmitAsync(BusinessClaim(member)));

        business.IsSuccess.Should().BeTrue();
        personal.IsSuccess.Should().BeTrue();
        personal.Value.IsDuplicate.Should().BeFalse();
    }

    // ── Validation ───────────────────────────────────────────────────────

    [Fact]
    public async Task An_unknown_verification_type_is_refused_rather_than_guessed_at()
    {
        var member = await RegisteredMemberAsync();

        var result = await WithSubmissionAsync(submission => submission.SubmitAsync(
            new TrustVerificationSubmissionRequest
            {
                VerificationType = "NotAThingTrustVerifies",
                SubmittedBy = member
            }));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("trust.verification.type-unsupported");
    }

    [Fact]
    public async Task A_submission_naming_no_member_is_refused()
    {
        var result = await WithSubmissionAsync(submission => submission.SubmitAsync(
            new TrustVerificationSubmissionRequest
            {
                VerificationType = "Business",
                SubmittedBy = Guid.Empty
            }));

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task A_claim_with_no_evidence_at_all_is_refused()
    {
        // Trust requires a document for Business. Submission does not relax that.
        var member = await RegisteredMemberAsync();

        var result = await WithSubmissionAsync(submission => submission.SubmitAsync(
            BusinessClaim(member, Guid.NewGuid(), evidence: [])));

        result.IsFailure.Should().BeTrue();
    }

    // ── The existing HTTP route is untouched ─────────────────────────────

    [Fact]
    public async Task The_members_own_diaspora_route_still_works_unchanged()
    {
        // B3's behaviour after the workflow moved into the shared coordinator. This is the
        // regression that would matter most: the route every member uses.
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/trust/verifications/diaspora",
            new
            {
                nationality = "NG",
                residenceCountry = "GB",
                documentType = "Passport",
                issueCountry = "NG",
                issueDate = "2020-01-01",
                expiryDate = "2030-01-01",
                documentNumber = "AA0012345",
                documentReference = "storage/doc-1",
                selfieReference = "storage/selfie-1"
            },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task The_members_own_route_still_refuses_a_duplicate_with_a_conflict()
    {
        // A person clicking submit twice gets a 409; only a module gets idempotent success.
        // Both paths run the same workflow and differ only in this policy.
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var payload = new
        {
            nationality = "NG",
            residenceCountry = "GB",
            documentType = "Passport",
            issueCountry = "NG",
            issueDate = "2020-01-01",
            expiryDate = "2030-01-01",
            documentNumber = "AA0012345",
            documentReference = "storage/doc-1",
            selfieReference = "storage/selfie-1"
        };

        var first = await HttpClient.PostAsJsonAsync(
            "/api/v1/trust/verifications/diaspora", payload, JsonOptions);

        var second = await HttpClient.PostAsJsonAsync(
            "/api/v1/trust/verifications/diaspora", payload, JsonOptions);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await second.Content.ReadAsStringAsync())
            .Should().Contain("trust.verification.duplicate");
    }

    [Fact]
    public async Task The_members_own_submission_records_no_subject()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        await HttpClient.PostAsJsonAsync(
            "/api/v1/trust/verifications/diaspora",
            new
            {
                nationality = "NG",
                residenceCountry = "GB",
                documentType = "Passport",
                issueCountry = "NG",
                issueDate = "2020-01-01",
                expiryDate = "2030-01-01",
                documentNumber = "AA0012345",
                documentReference = "storage/doc-1",
                selfieReference = "storage/selfie-1"
            },
            JsonOptions);

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var stored = await context.Set<VerificationRequest>()
            .Where(request => request.UserId == UserId.From(auth.User.Id))
            .ToListAsync();

        stored.Should().NotBeEmpty();
        stored.Should().OnlyContain(request => request.SubjectType == null,
            "the member is the subject, recorded exactly as B3 recorded it");
        stored.Should().OnlyContain(request => request.CorrelationId == null);
    }
}
