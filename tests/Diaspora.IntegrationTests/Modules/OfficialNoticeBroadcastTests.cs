using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Requests;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Contracts.Intelligence.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.Verification;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Notifications.Application.Consumers.Intelligence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// A published official notice reaches the members living in its country, and only them.
/// <para>
/// <b>Added 8 Oct 2026.</b> A platform admin published a notice for Mauritius and no member saw it:
/// nothing consumed <c>OfficialNoticePublished</c>, and the "everyone in a country" audience every
/// broadcast rule relied on was an implementation that answered nobody.
/// </para>
/// <para>
/// The member lives in Tuvalu because every other test member lives in Canada, and the audience is
/// capped: a new Canadian could fall past the cap and make this test pass or fail on test order.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class OfficialNoticeBroadcastTests : PooledIdentityTestBase
{
    private const string Country = "TV";

    public OfficialNoticeBroadcastTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_member_living_in_the_country_is_told_and_nobody_else_is()
    {
        var resident = await RegisterInAsync(Country);
        var elsewhere = await RegisterInAsync("FJ");

        var title = $"Passport renewal {Guid.NewGuid():N}";

        await PublishNoticeAsync(Country, title);

        (await InboxTitlesAsync(resident)).Should().Contain(t => t.Contains(title));
        (await InboxTitlesAsync(elsewhere)).Should().NotContain(t => t.Contains(title));
    }

    private async Task PublishNoticeAsync(string country, string title)
    {
        using var scope = Factory.Services.CreateScope();

        var consumer = ActivatorUtilities.CreateInstance<OfficialNoticePublishedConsumer>(scope.ServiceProvider);

        await consumer.Handle(
            new IntegrationEventNotification<OfficialNoticePublishedIntegrationEvent>(
                new OfficialNoticePublishedIntegrationEvent
                {
                    OfficialNoticeId = Guid.NewGuid(),
                    CountryCode = country,
                    CategoryKey = "embassy-notices",
                    Title = title,
                    Summary = "You can now renew your passport without travelling home.",
                    Severity = "Informational",
                    AuthorityId = Guid.NewGuid(),
                    AuthorityName = "Diaspora platform",
                    IsOfficial = false,
                    PublishedAt = DateTimeOffset.UtcNow
                }),
            CancellationToken.None);

        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
    }

    private async Task<string> RegisterInAsync(string country)
    {
        var phone = NextPhoneNumber();

        await IdentityApiClient.ReadDataAsync<RegistrationResponse>(
            await Api.RegisterAsync(RegistrationFor(phone) with { CountryOfResidence = country }));

        var auth = await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(
            await Api.VerifyPhoneAsync(new VerifyPhoneRequest
            {
                PhoneNumber = phone,
                Code = Factory.OtpCodes.LatestFor(phone, OtpPurpose.PhoneVerification),
                Platform = "Android",
                AppVersion = "1.0.0"
            }));

        return auth.Tokens.AccessToken;
    }

    private async Task<IReadOnlyList<string>> InboxTitlesAsync(string accessToken)
    {
        Api.Authenticate(accessToken);

        var body = await HttpClient.GetStringAsync("/api/v1/notifications?limit=50");

        // Titles anywhere in the page; the shape of the envelope is not what this test is about.
        return System.Text.RegularExpressions.Regex
            .Matches(body, "\"title\"\\s*:\\s*\"([^\"]*)\"")
            .Select(match => match.Groups[1].Value)
            .ToList();
    }
}
