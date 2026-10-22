using System.Net;
using System.Text;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// <see cref="ReadinessProbe"/> decides what counts as the harness's artefact rather than a
/// result, so its one rule is pinned here: a check that threw is read again; a check that
/// evaluated — healthy or not — is never second-guessed.
/// </summary>
public sealed class ReadinessProbeTests
{
    private const string Evaluated =
        """{"status":"Healthy","checks":[{"name":"outbox","status":"Healthy","data":{"pending":0},"error":null}]}""";

    private const string EvaluatedUnhealthy =
        """{"status":"Unhealthy","checks":[{"name":"outbox","status":"Unhealthy","data":{"pending":9},"error":null}]}""";

    private const string Thrown =
        """{"status":"Unhealthy","checks":[{"name":"outbox","status":"Unhealthy","data":null,"error":"SQLite Error 5: 'database is locked'."}]}""";

    [Fact]
    public async Task A_check_that_threw_is_read_again()
    {
        var (client, handler) = Client(Thrown, Thrown, Evaluated);

        var outbox = await client.ReadinessCheckAsync("outbox");

        outbox.GetProperty("status").GetString().Should().Be("Healthy");
        handler.Calls.Should().Be(3);
    }

    [Fact]
    public async Task An_evaluated_unhealthy_check_is_returned_on_the_first_read()
    {
        var (client, handler) = Client(EvaluatedUnhealthy, Evaluated);

        var outbox = await client.ReadinessCheckAsync("outbox");

        outbox.GetProperty("status").GetString().Should().Be("Unhealthy", "a real result is never retried away");
        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task A_check_that_always_throws_fails_with_its_own_error()
    {
        var (client, _) = Client(Thrown, Thrown, Thrown, Thrown, Thrown);

        var reading = () => client.ReadinessCheckAsync("outbox");

        await reading.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*database is locked*");
    }

    private static (HttpClient Client, SequenceHandler Handler) Client(params string[] bodies)
    {
        var handler = new SequenceHandler(bodies);
        return (new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }, handler);
    }

    private sealed class SequenceHandler(string[] bodies) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = bodies[Math.Min(Calls, bodies.Length - 1)];
            Calls++;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
