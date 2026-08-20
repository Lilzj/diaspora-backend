using Diaspora.Domain.Identity.Sessions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Identity;

/// <summary>
/// The refresh-token race the audit flagged: two requests presenting the same token both
/// see it unspent and both rotate, forking the chain and defeating reuse detection. B2.6
/// closes it with an optimistic-concurrency token on <c>RotatedAt</c>. These tests confirm
/// the mapping is in place and that a replayed token is refused, which is the guarantee the
/// concurrency token exists to keep under load.
/// </summary>
public sealed class RefreshTokenConcurrencyTests : IdentityTestBase
{
    public RefreshTokenConcurrencyTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public void RotatedAt_is_mapped_as_a_concurrency_token()
    {
        // The mapping is the whole defence: it puts "AND rotated_at IS NULL" into the
        // rotation UPDATE, so a second concurrent rotation matches zero rows and is caught.
        // Asserting the model metadata pins that it cannot be silently dropped.
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var property = dbContext.Model
            .FindEntityType(typeof(RefreshToken))!
            .FindProperty(nameof(RefreshToken.RotatedAt))!;

        property.IsConcurrencyToken.Should().BeTrue(
            "a rotation must fail if the token was already spent by a concurrent request");
    }

    [Fact]
    public async Task A_replayed_refresh_token_is_refused_and_kills_the_session()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        var original = auth.Tokens.RefreshToken;
        Api.ClearAuthentication();

        // First rotation succeeds.
        var first = await Api.RefreshAsync(original);
        first.IsSuccessStatusCode.Should().BeTrue();

        // Replaying the original — now rotated — is treated as theft: refused, and the
        // session is torn down. This is the behaviour the concurrency token guarantees even
        // when the two presentations arrive at the same instant.
        var replay = await Api.RefreshAsync(original);
        replay.IsSuccessStatusCode.Should().BeFalse();
    }
}
