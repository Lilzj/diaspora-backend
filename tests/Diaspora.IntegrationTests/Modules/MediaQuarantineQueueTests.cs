using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.SharedKernel.Identity;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.Domain.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The quarantine queue, and the reason it had to exist.
/// <para>
/// <b>Releasing a quarantine and requeueing processing both take a media identifier, and until
/// this route landed nothing in the platform produced one.</b> A moderator could act on a
/// refused file only if somebody handed them its id out of band, which meant in practice that
/// nothing was ever released and a member's upload disappeared with no way back. A control
/// nobody can reach is not a control, and the tests below are about reachability before they
/// are about anything else.
/// </para>
/// <para>
/// Malware scanning is switched off in this host, so the verdict is written against the
/// aggregate directly — through <c>RecordScan</c>, the same method the scanner calls, rather
/// than by setting a column. What is being tested is the queue, not the scanner.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class MediaQuarantineQueueTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/media";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>A one-pixel PNG. Real bytes, so the signature check has something to read.</summary>
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    public MediaQuarantineQueueTests(CustomWebApplicationFactory factory) : base(factory) { }

    /// <summary>The queue lists a refused file, with what the scanner said about it.</summary>
    [Fact]
    public async Task A_refused_file_is_reachable_with_what_the_scanner_said()
    {
        var mediaId = await QuarantinedUploadAsync();

        await AuthenticateAsAdministratorAsync();

        var listed = await ReadDataAsync<List<QuarantinedDto>>(
            await HttpClient.GetAsync($"{Root}/quarantine"));

        var row = listed.Should().ContainSingle(entry => entry.MediaId == mediaId).Subject;

        row.ScanStatus.Should().Be("Infected");
        row.ThreatName.Should().Be("Eicar-Test-Signature");
        row.Scanner.Should().Be("integration-scanner");
    }

    /// <summary>
    /// And the identifier it hands over is one the release route accepts — the whole point.
    /// </summary>
    [Fact]
    public async Task The_identifier_the_queue_gives_is_one_the_release_route_takes()
    {
        var mediaId = await QuarantinedUploadAsync();

        await AuthenticateAsAdministratorAsync();

        var listed = await ReadDataAsync<List<QuarantinedDto>>(
            await HttpClient.GetAsync($"{Root}/quarantine"));

        var released = await HttpClient.PostAsJsonAsync(
            $"{Root}/{listed.Single(entry => entry.MediaId == mediaId).MediaId}/quarantine/release",
            new { reason = "Confirmed false positive against the vendor's own sample." });

        released.StatusCode.Should().Be(HttpStatusCode.OK);

        var after = await ReadDataAsync<List<QuarantinedDto>>(
            await HttpClient.GetAsync($"{Root}/quarantine"));

        after.Should().NotContain(entry => entry.MediaId == mediaId);
    }

    /// <summary>
    /// A member cannot read the queue. It is other people's refused uploads, which is a broader
    /// disclosure than acting on one you already know about — hence the harder gate.
    /// </summary>
    [Fact]
    public async Task A_member_cannot_read_other_members_refused_uploads()
    {
        await QuarantinedUploadAsync();

        var response = await HttpClient.GetAsync($"{Root}/quarantine");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>An uploaded file the scanner then refused.</summary>
    private async Task<Guid> QuarantinedUploadAsync()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.Tokens.AccessToken);

        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(OnePixelPng);

        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "pixel.png");
        form.Add(new StringContent("image"), "kind");

        var uploaded = await HttpClient.PostAsync($"{Root}/uploads/direct", form);
        uploaded.StatusCode.Should().Be(HttpStatusCode.Created);

        var mediaId = (await ReadDataAsync<UploadedDto>(uploaded)).MediaId;

        using var scope = Factory.Services.CreateScope();

        var assets = scope.ServiceProvider.GetRequiredService<IMediaAssetRepository>();
        var asset = await assets.GetAsync(MediaId.From(mediaId));

        asset.Should().NotBeNull();

        asset!.RecordScan(VirusScanResult.Infected(
            "integration-scanner", "Eicar-Test-Signature", DateTimeOffset.UtcNow));

        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        return mediaId;
    }

    /// <summary>A real Admin — the only role carrying both media gates.</summary>
    private async Task AuthenticateAsAdministratorAsync() => await SignInAsRoleAsync("Admin");

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<T>>(JsonOptions);

        return envelope!.Data!;
    }

    private sealed record Envelope<T>(T? Data);

    private sealed record UploadedDto(Guid MediaId);

    private sealed record QuarantinedDto(
        Guid MediaId,
        string FileName,
        string ScanStatus,
        string? Scanner,
        string? ThreatName,
        string OwnerType);
}
