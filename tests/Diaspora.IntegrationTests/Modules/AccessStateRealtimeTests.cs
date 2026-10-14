using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Notifications.Application.Consumers.Identity;
using Diaspora.Modules.Notifications.Extensibility.Realtime;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// A member's open app hears that their access changed, over the socket (14 Oct 2026). Replaces
/// the app asking every ten seconds while a verification decision was awaited.
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class AccessStateRealtimeTests : PooledIdentityTestBase
{
    private const string HubPath = "/hubs/realtime";

    public AccessStateRealtimeTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task An_access_change_reaches_the_members_own_socket_and_nobody_elses()
    {
        var (_, member) = await RegisterVerifiedMemberAsync();
        var (_, bystander) = await RegisterVerifiedMemberAsync();

        var memberConnection = await ConnectedAsync(member);
        var bystanderConnection = await ConnectedAsync(bystander);

        var memberFrames = new TaskCompletionSource<ResourceChangedShape>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bystanderFrames = new List<ResourceChangedShape>();
        memberConnection.On<ResourceChangedShape>("ResourceChanged", frame => memberFrames.TrySetResult(frame));
        bystanderConnection.On<ResourceChangedShape>("ResourceChanged", frame => bystanderFrames.Add(frame));

        try
        {
            using (var scope = Factory.Services.CreateScope())
            {
                var relay = new AccessStateRealtimeRelay(scope.ServiceProvider.GetRequiredService<IRealtimeNotifier>());

                await relay.Handle(
                    new IntegrationEventNotification<UserAccessStateChangedIntegrationEvent>(
                        new UserAccessStateChangedIntegrationEvent
                        {
                            UserId = member.User.Id,
                            FromState = "VerificationUnderReview",
                            ToState = "DiasporaVerified",
                            Reason = "VerificationApproved"
                        }),
                    CancellationToken.None);
            }

            var arrived = await Task.WhenAny(memberFrames.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            arrived.Should().BeSameAs(memberFrames.Task, "the member's own socket never heard the change");

            var frame = await memberFrames.Task;
            frame.ResourceType.Should().Be("account");
            frame.ChangeKind.Should().Be("access.changed");
            frame.ResourceId.Should().Be(member.User.Id.ToString());

            await Task.Delay(300);
            bystanderFrames.Should().BeEmpty("another member's access is nobody else's business");
        }
        finally
        {
            await DisconnectAsync(memberConnection, member);
            await DisconnectAsync(bystanderConnection, bystander);
        }
    }

    public sealed record ResourceChangedShape(
        string ResourceType, string ResourceId, string ChangeKind, string? Reason, DateTimeOffset OccurredAt);

    private async Task<HubConnection> ConnectedAsync(Contracts.Identity.V1.Responses.AuthenticationResponse auth)
    {
        var acknowledged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // WebSockets, as AlertRealtimeChannelTests explains: long polling re-authenticates on every
        // poll and collides with the suite's single in-memory SQLite connection.
        var connection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(Factory.Server.BaseAddress, HubPath.TrimStart('/')),
                options =>
                {
                    options.Transports = HttpTransportType.WebSockets;
                    options.SkipNegotiation = true;
                    options.AccessTokenProvider = () => Task.FromResult<string?>(auth.Tokens.AccessToken);
                    options.WebSocketFactory = async (context, cancellationToken) =>
                    {
                        var client = Factory.Server.CreateWebSocketClient();
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

    private async Task DisconnectAsync(
        HubConnection connection, Contracts.Identity.V1.Responses.AuthenticationResponse auth)
    {
        await connection.StopAsync();
        await connection.DisposeAsync();

        var registry = Factory.Services.GetRequiredService<IRealtimeConnectionRegistry>();
        var userId = SharedKernel.Identity.UserId.From(auth.User.Id);

        for (var attempt = 0; attempt < 50 && await registry.IsConnectedAsync(userId); attempt++)
        {
            await Task.Delay(100);
        }
    }
}
