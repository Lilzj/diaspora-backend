using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Platform.Communication;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The live alert map, end to end: a phone watching a map cell hears about an alert the moment it
/// changes — 16 Sep 2026.
/// <para>
/// Every hop is the real one: the alert is published over HTTP, the domain event becomes an
/// integration event on the outbox, the outbox is drained exactly as the dispatcher would, the
/// Notifications consumer fans the signal out to cells, and a SignalR client joined to one of them
/// receives it. A test that called the broadcaster directly would pass with the Community handlers
/// never registered — which is the commonest defect on this platform: right code, no producer.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class AlertRealtimeChannelTests : PooledIdentityTestBase
{
    private const string HubPath = "/hubs/realtime";
    private const double PeckhamLatitude = 51.4742d;
    private const double PeckhamLongitude = -0.0691d;

    private static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web);

    public AlertRealtimeChannelTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_member_may_watch_a_map_cell_and_not_a_name_that_is_no_cell()
    {
        var auth = await VerifiedMemberAsync();

        var connection = await ConnectedAsync(auth);
        await using var teardown = new Teardown(connection, auth, Factory);

        (await connection.InvokeAsync<bool>("JoinGroup", "geo:alerts:gcpv")).Should().BeTrue();
        (await connection.InvokeAsync<bool>("JoinGroup", "geo:alerts:london")).Should().BeFalse(
            "a name that is not a cell is a group nothing will ever publish to");
    }

    [Fact]
    public async Task A_published_alert_reaches_a_member_watching_its_cell()
    {
        var auth = await VerifiedMemberAsync();
        var connection = await ConnectedAsync(auth);
        await using var teardown = new Teardown(connection, auth, Factory);

        var frames = Listen(connection);
        var cell = GeoCells.Encode(PeckhamLatitude, PeckhamLongitude);

        (await connection.InvokeAsync<bool>("JoinGroup", RealtimeChannels.ForAlertCell(cell))).Should().BeTrue();

        var postId = await PublishAlertAsync(PeckhamLatitude, PeckhamLongitude, radius: null);

        await Factory.DrainOutboxAsync();

        var frame = await frames.WaitForAsync(postId);

        frame.ResourceType.Should().Be("alert");
        frame.ChangeKind.Should().Be("created");
    }

    [Fact]
    public async Task A_withdrawal_reaches_the_cell_as_a_status_change_with_its_reason()
    {
        var auth = await VerifiedMemberAsync();
        var postId = await PublishAlertAsync(PeckhamLatitude, PeckhamLongitude, radius: null);
        await Factory.DrainOutboxAsync();

        var connection = await ConnectedAsync(auth);
        await using var teardown = new Teardown(connection, auth, Factory);
        var frames = Listen(connection);

        await connection.InvokeAsync<bool>(
            "JoinGroup", RealtimeChannels.ForAlertCell(GeoCells.Encode(PeckhamLatitude, PeckhamLongitude)));

        (await HttpClient.PostAsync($"/api/v1/community/posts/{postId}/withdraw", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await Factory.DrainOutboxAsync();

        var frame = await frames.WaitForAsync(postId);

        frame.ChangeKind.Should().Be("status.changed");
        frame.Reason.Should().Be("withdrawn");
    }

    /// <summary>
    /// The member watching the cell east of Peckham is outside the pin's cell and inside the alert's
    /// 20 km area, so they are told.
    /// </summary>
    [Fact]
    public async Task An_alert_reaches_a_neighbouring_cell_its_area_covers()
    {
        var auth = await VerifiedMemberAsync();
        var connection = await ConnectedAsync(auth);
        await using var teardown = new Teardown(connection, auth, Factory);
        var frames = Listen(connection);

        var east = GeoCells.Encode(PeckhamLatitude, 0.14d);

        east.Should().NotBe(GeoCells.Encode(PeckhamLatitude, PeckhamLongitude));
        (await connection.InvokeAsync<bool>("JoinGroup", RealtimeChannels.ForAlertCell(east))).Should().BeTrue();

        var postId = await PublishAlertAsync(PeckhamLatitude, PeckhamLongitude, radius: 20_000);

        await Factory.DrainOutboxAsync();

        (await frames.WaitForAsync(postId)).ChangeKind.Should().Be("created");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────

    private async Task<Contracts.Identity.V1.Responses.AuthenticationResponse> VerifiedMemberAsync()
    {
        Factory.EnsureDatabaseCreated();

        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        return auth;
    }

    private async Task<Guid> PublishAlertAsync(double latitude, double longitude, int? radius)
    {
        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/community/posts",
            new
            {
                body = $"Water main burst by the station. {Guid.NewGuid():N}",
                category = "traffic",
                urgency = "urgent",
                countryCode = "GB",
                latitude,
                longitude,
                affectedRadiusMetres = radius,
                publish = true
            },
            WireJson);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        return (await IdentityApiClient.ReadDataAsync<CreatedShape>(response)).PostId;
    }

    private async Task<HubConnection> ConnectedAsync(Contracts.Identity.V1.Responses.AuthenticationResponse auth)
    {
        var acknowledged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var connection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(Factory.Server.BaseAddress, HubPath.TrimStart('/')),
                options =>
                {
                    // WebSockets rather than the long polling CommunicationRuntimeTests uses, because
                    // these tests make HTTP calls while connected. A long-polling client authenticates
                    // again on every poll, and on this host every request shares one in-memory SQLite
                    // connection — a poll landing mid-request fails with "unable to delete/modify
                    // user-function due to active statements". A socket authenticates once.
                    options.Transports = HttpTransportType.WebSockets;
                    options.SkipNegotiation = true;
                    options.AccessTokenProvider = () => Task.FromResult<string?>(auth.Tokens.AccessToken);
                    options.WebSocketFactory = async (context, cancellationToken) =>
                    {
                        var client = Factory.Server.CreateWebSocketClient();

                        // The test server's socket client carries no token of its own; the hub reads
                        // the bearer header on the upgrade request.
                        client.ConfigureRequest = request =>
                            request.Headers.Authorization = $"Bearer {auth.Tokens.AccessToken}";

                        return await client.ConnectAsync(context.Uri, cancellationToken);
                    };
                })
            .Build();

        connection.On<object>("ConnectionAcknowledged", _ => acknowledged.TrySetResult());

        await connection.StartAsync();

        var completed = await Task.WhenAny(acknowledged.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        completed.Should().BeSameAs(acknowledged.Task, "the hub never acknowledged the connection");

        return connection;
    }

    /// <summary>
    /// Stops the connection and waits until the server has let it go.
    /// <para>
    /// The suite shares one in-memory SQLite connection, and a hub connection disposed at the end of
    /// a test is torn down on the server after the test has returned — writing its disconnect while
    /// the next test's requests are running, which SQLite refuses. The next test then fails with a
    /// 500 that has nothing to do with it. Waiting for the registry to agree the member is gone makes
    /// each test leave the host as it found it.
    /// </para>
    /// </summary>
    private sealed class Teardown(
        HubConnection connection,
        Contracts.Identity.V1.Responses.AuthenticationResponse auth,
        CustomWebApplicationFactory factory) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await connection.StopAsync();
            await connection.DisposeAsync();

            var registry = factory.Services
                .GetRequiredService<Diaspora.Modules.Notifications.Extensibility.Realtime.IRealtimeConnectionRegistry>();
            var userId = SharedKernel.Identity.UserId.From(auth.User.Id);

            for (var attempt = 0; attempt < 50 && await registry.IsConnectedAsync(userId); attempt++)
            {
                await Task.Delay(100);
            }
        }
    }

    private static FrameListener Listen(HubConnection connection)
    {
        var listener = new FrameListener();
        connection.On<ResourceChangedShape>("ResourceChanged", listener.Receive);

        return listener;
    }

    /// <summary>Collects frames, and waits for the one about a given alert.</summary>
    private sealed class FrameListener
    {
        private readonly List<ResourceChangedShape> _frames = [];
        private readonly Lock _gate = new();

        public void Receive(ResourceChangedShape frame)
        {
            lock (_gate)
            {
                _frames.Add(frame);
            }
        }

        public async Task<ResourceChangedShape> WaitForAsync(Guid postId)
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(10);

            while (DateTimeOffset.UtcNow < deadline)
            {
                lock (_gate)
                {
                    var match = _frames.LastOrDefault(frame =>
                        string.Equals(frame.ResourceId, postId.ToString("D"), StringComparison.OrdinalIgnoreCase));

                    if (match is not null)
                    {
                        return match;
                    }
                }

                await Task.Delay(100);
            }

            throw new TimeoutException($"No frame about alert {postId} arrived on the watched cell.");
        }
    }

    private sealed record CreatedShape
    {
        public Guid PostId { get; init; }
    }

    private sealed record ResourceChangedShape
    {
        public string ResourceType { get; init; } = string.Empty;

        public string ResourceId { get; init; } = string.Empty;

        public string ChangeKind { get; init; } = string.Empty;

        public string? Reason { get; init; }

        public DateTimeOffset OccurredAt { get; init; }
    }
}
