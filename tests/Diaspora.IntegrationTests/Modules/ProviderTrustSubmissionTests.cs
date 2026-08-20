using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Providers.Application.Verification;
using Diaspora.Modules.Providers.Contracts.V1.Requests;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The loop Gap 2 left open, closed end to end.
/// <para>
/// B7 has always been able to <em>receive</em> a Trust decision — the four outcome handlers were
/// complete and tested from the day it shipped. What it could not do was raise a claim, so the
/// identifier it stored was one it made up and no decision could ever match it. These tests
/// assert the one property that was missing: <b>the identifier B7 records is the identifier Trust
/// holds</b>. Everything downstream already worked and still does.
/// </para>
/// </summary>
public sealed class ProviderTrustSubmissionTests : IdentityTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ProviderTrustSubmissionTests(CustomWebApplicationFactory factory) : base(factory) { }

    private async Task<Guid> RegisterProviderAsync(string name)
    {
        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/providers",
            new Dictionary<string, object?>
            {
                ["name"] = name,
                ["providerType"] = "Business",
                ["category"] = "Restaurants",
                ["cityName"] = "London",
                ["countryCode"] = "GB",
                ["phone"] = "+447700900456"
            },
            JsonOptions);

        response.IsSuccessStatusCode.Should().BeTrue(
            await response.Content.ReadAsStringAsync());

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("data").GetProperty("providerId").GetGuid();
    }

    /// <summary>
    /// Dispatches through MediatR so the command runs under the real pipeline — validation,
    /// authorisation and the transaction behavior all apply, exactly as they would for a route.
    /// </summary>
    private async Task<T> SendAsync<T>(IRequest<T> command, string accessToken)
    {
        using var scope = Factory.Services.CreateScope();

        // The command reads the acting member from ICurrentUser, so the scope needs one.
        ScopedCurrentUser.Apply(scope.ServiceProvider, accessToken);

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(command);
    }

    [Fact]
    public async Task A_provider_claim_opens_a_case_trust_actually_holds()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var providerId = await RegisterProviderAsync("Naija Express Money Transfer");

        var submitted = await SubmitAsync(providerId, auth.Tokens.AccessToken);

        submitted.IsSuccess.Should().BeTrue(submitted.IsFailure ? submitted.Error.Code : null);

        var trustVerificationId = submitted.Value.TrustVerificationId;
        trustVerificationId.Should().NotBeEmpty();

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        // The assertion the stub could never satisfy. Before B3.0.2 this identifier was a fresh
        // Guid that matched nothing in the trust schema.
        var trustCase = await context.Set<VerificationRequest>()
            .FirstOrDefaultAsync(request =>
                request.Id == VerificationRequestId.From(trustVerificationId));

        trustCase.Should().NotBeNull("B7's identifier must be a case Trust really opened");
        trustCase!.Type.Should().Be(VerificationType.Business);
        trustCase.SubjectType.Should().Be("Provider");
        trustCase.SubjectId.Should().Be(providerId);
        trustCase.RequestedBy.Should().Be("Providers");
        trustCase.Status.Should().Be(VerificationStatus.Pending);
    }

    [Fact]
    public async Task The_provider_stores_the_identifier_trust_returned()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var providerId = await RegisterProviderAsync("Ebony Braids and Beauty");

        var submitted = await SubmitAsync(providerId, auth.Tokens.AccessToken);

        submitted.IsSuccess.Should().BeTrue(submitted.IsFailure ? submitted.Error.Code : null);

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var provider = await context.Set<Provider>()
            .Include(candidate => candidate.Verifications)
            .FirstAsync(candidate => candidate.Id == ProviderId.From(providerId));

        var claim = provider.Verifications
            .Should().ContainSingle(candidate => candidate.Kind == ProviderVerificationKind.Business)
            .Subject;

        // Both halves of the correlation, asserted together. This pair is what lets a decision
        // find the claim that raised it.
        claim.TrustVerificationId.Should().Be(submitted.Value.TrustVerificationId);
        claim.State.Should().Be(ProviderVerificationState.Pending,
            "submission says 'we have asked', never 'we have been granted'");
        claim.SubmittedBy.Should().NotBeNull();
    }

    [Fact]
    public async Task A_second_claim_while_one_is_pending_is_refused_by_the_provider()
    {
        // B7's own rule, unchanged by B3.0.2. Trust's subject check would also catch it; the
        // aggregate refuses first, which is the cheaper answer and the one with the better
        // message.
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var providerId = await RegisterProviderAsync("Lagos Legal Services");

        var first = await SubmitAsync(providerId, auth.Tokens.AccessToken);
        var second = await SubmitAsync(providerId, auth.Tokens.AccessToken);

        first.IsSuccess.Should().BeTrue(first.IsFailure ? first.Error.Code : null);
        second.IsFailure.Should().BeTrue("one live claim per provider and kind");
        second.Error.Code.Should().Be("providers.verification.already-pending");
    }

    [Fact]
    public async Task Two_providers_owned_by_one_member_are_two_separate_claims()
    {
        // The case that made subjects necessary. Under Trust's old member-scoped duplicate rule
        // the second of these would have been refused as a duplicate of the first.
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var first = await RegisterProviderAsync("Accra Auto Repairs");
        var second = await RegisterProviderAsync("Accra Auto Parts");

        var firstClaim = await SubmitAsync(first, auth.Tokens.AccessToken);
        var secondClaim = await SubmitAsync(second, auth.Tokens.AccessToken);

        firstClaim.IsSuccess.Should().BeTrue(firstClaim.IsFailure ? firstClaim.Error.Code : null);
        secondClaim.IsSuccess.Should().BeTrue(secondClaim.IsFailure ? secondClaim.Error.Code : null);

        secondClaim.Value.TrustVerificationId
            .Should().NotBe(firstClaim.Value.TrustVerificationId,
                "one member with two businesses is making two claims, not one twice");
    }

    private Task<SharedKernel.Results.Result<ProviderVerificationClaimAccepted>> SubmitAsync(
        Guid providerId, string accessToken) =>
        SendAsync(
            new SubmitProviderVerificationCommand
            {
                ProviderId = providerId,
                Request = new SubmitProviderVerificationRequest
                {
                    Kind = "Business",
                    EvidenceMediaIds = [Guid.NewGuid()]
                }
            },
            accessToken);
}
