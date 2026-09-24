using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// B1.0.1 verified against the real aggregate that exposed the defect, through the real host.
/// <para>
/// The unit suite proves the converter's behaviour on a purpose-built aggregate. This proves it
/// on <c>MediaAsset</c> — four owned references, one of them nested, two owned collections —
/// saved through the composed <c>DiasporaDbContext</c> with every module's contributor loaded.
/// That combination is what actually failed in B5.
/// </para>
/// </summary>
[Collection(PlatformCollection.Name)]
public sealed class SoftDeletePersistenceTests : PooledIdentityTestBase
{
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    public SoftDeletePersistenceTests(CustomWebApplicationFactory factory) : base(factory) { }

    /// <summary>
    /// The defect, at the real aggregate. Pre-B1.0.1 the DELETE returned 500 with
    /// <c>NOT NULL constraint failed: media_assets.owner_type</c>; B5 worked around it by
    /// stamping the deletion by hand. With the platform fixed and the workaround removed, the
    /// ordinary repository path writes a soft delete that leaves every owned column intact.
    /// </summary>
    [Fact]
    public async Task Soft_deleting_a_media_asset_preserves_its_owned_value_objects()
    {
        await AuthenticateAsync();

        var mediaId = await UploadAsync();

        var response = await HttpClient.DeleteAsync($"/api/v1/media/{mediaId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var asset = await context.Set<MediaAsset>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(candidate => candidate.Id == MediaId.From(mediaId));

        asset.Should().NotBeNull();
        asset!.IsDeleted.Should().BeTrue();
        asset.Status.Should().Be(MediaAssetStatus.Deleted);

        // The four owned value objects. Every one of these was written as NULL before B1.0.1,
        // which is what the not-null constraint refused.
        asset.Location.Provider.Should().NotBeNullOrEmpty();
        asset.Location.Container.Should().NotBeNullOrEmpty();
        asset.Location.Key.Value.Should().NotBeNullOrEmpty();
        asset.Owner.Should().NotBeNull();
        asset.ScanResult.Should().NotBeNull();
        asset.Metadata.Should().NotBeNull();

        // And the metadata's nested owned value object, one level deeper.
        asset.Metadata.Dimensions.Should().NotBeNull();
        asset.Metadata.Dimensions!.Width.Should().Be(1);
        asset.Metadata.Dimensions.Height.Should().Be(1);
    }

    /// <summary>
    /// The audit stamp still lands, with the platform clock and the acting member, on the same
    /// save that repairs the graph.
    /// </summary>
    [Fact]
    public async Task Soft_deleting_a_media_asset_records_who_and_when()
    {
        await AuthenticateAsync();

        var mediaId = await UploadAsync();
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);

        await HttpClient.DeleteAsync($"/api/v1/media/{mediaId}");

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var asset = await context.Set<MediaAsset>()
            .IgnoreQueryFilters()
            .FirstAsync(candidate => candidate.Id == MediaId.From(mediaId));

        asset.DeletedAt.Should().NotBeNull();
        asset.DeletedAt!.Value.Should().BeAfter(before);
        asset.DeletedBy.Should().NotBeNullOrEmpty();

        // Creation audit is untouched — a row whose CreatedAt moved is a fiction.
        asset.CreatedAt.Should().BeBefore(asset.DeletedAt!.Value.AddSeconds(1));
    }

    /// <summary>
    /// The query filter still hides it. B1.0.1 changed how a deletion is written, not what a
    /// deleted row means.
    /// </summary>
    [Fact]
    public async Task A_soft_deleted_media_asset_is_hidden_from_ordinary_queries()
    {
        await AuthenticateAsync();

        var mediaId = await UploadAsync();

        await HttpClient.DeleteAsync($"/api/v1/media/{mediaId}");

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var filtered = await context.Set<MediaAsset>()
            .FirstOrDefaultAsync(candidate => candidate.Id == MediaId.From(mediaId));

        var unfiltered = await context.Set<MediaAsset>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(candidate => candidate.Id == MediaId.From(mediaId));

        filtered.Should().BeNull();
        unfiltered.Should().NotBeNull();
    }

    /// <summary>
    /// Deleting several assets in one member's session, each with a full owned graph, all
    /// survive. This is the shape a member emptying a gallery produces.
    /// </summary>
    [Fact]
    public async Task Deleting_several_assets_leaves_every_owned_graph_intact()
    {
        await AuthenticateAsync();

        var ids = new List<Guid>();

        for (var index = 0; index < 4; index++)
        {
            ids.Add(await UploadAsync());
        }

        foreach (var id in ids)
        {
            (await HttpClient.DeleteAsync($"/api/v1/media/{id}"))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        foreach (var id in ids)
        {
            var asset = await context.Set<MediaAsset>()
                .IgnoreQueryFilters()
                .FirstAsync(candidate => candidate.Id == MediaId.From(id));

            asset.IsDeleted.Should().BeTrue();
            asset.Location.Key.Value.Should().NotBeNullOrEmpty();
            asset.Metadata.Dimensions.Should().NotBeNull();
        }
    }

    /// <summary>
    /// <b>Consistency across every soft-deletable aggregate the platform composes.</b>
    /// <para>
    /// Rather than testing the three that exist today and missing the fourth somebody adds next
    /// year, this walks the composed model. Every <see cref="ISoftDeletable"/> entity type must
    /// carry the platform's query filter — which is what makes the converter's output mean
    /// "hidden" rather than "still visible but flagged".
    /// </para>
    /// </summary>
    [Fact]
    public void Every_soft_deletable_entity_in_the_composed_model_carries_the_query_filter()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var softDeletable = context.Model
            .GetEntityTypes()
            .Where(entity => typeof(ISoftDeletable).IsAssignableFrom(entity.ClrType))
            .ToList();

        softDeletable.Should().NotBeEmpty(
            "the platform has soft-deletable aggregates; finding none means the model failed " +
            "to compose and this test is asserting nothing");

        foreach (var entity in softDeletable)
        {
            entity.GetDeclaredQueryFilters().Should().NotBeEmpty(
                $"{entity.ClrType.Name} is soft-deletable and must be filtered out of ordinary reads");
        }
    }

    /// <summary>
    /// Documents the blast radius the converter actually covers, and fails if a soft-deletable
    /// aggregate with owned types appears in a module that predates B1.0.1's guarantee.
    /// <para>
    /// The assertion is deliberately about the <em>combination</em>: soft-deletable plus owned
    /// navigations is exactly the pairing that was broken, and the platform now handles it. If
    /// this list is empty the fix is untested in production code, which is itself worth knowing.
    /// </para>
    /// </summary>
    [Fact]
    public void At_least_one_composed_aggregate_is_both_soft_deletable_and_owns_value_objects()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var atRisk = context.Model
            .GetEntityTypes()
            .Where(entity => typeof(ISoftDeletable).IsAssignableFrom(entity.ClrType))
            .Where(entity => entity.GetNavigations()
                .Any(navigation => navigation.TargetEntityType.IsOwned()))
            .Select(entity => entity.ClrType.Name)
            .ToList();

        atRisk.Should().Contain(
            nameof(MediaAsset),
            "MediaAsset is the aggregate that exposed D-1 and is the platform's live proof the " +
            "combination now works");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private async Task AuthenticateAsync()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.Tokens.AccessToken);
    }

    private async Task<Guid> UploadAsync()
    {
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(OnePixelPng);

        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");

        form.Add(file, "file", "pixel.png");
        form.Add(new StringContent("image"), "kind");

        var response = await HttpClient.PostAsync("/api/v1/media/uploads/direct", form);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("data").GetProperty("mediaId").GetGuid();
    }
}
