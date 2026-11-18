using System.Net;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Resilience;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Platform.Resilience;

/// <summary>
/// The handler that gave <c>financial.payment-provider</c> a caller.
/// <para>
/// <b>BARCH-007 is recorded as "no circuit breakers" and that is not what was wrong.</b> The
/// platform has had a breaker since B17, with an operator dashboard, isolate and reset controls, a
/// health contributor, and a declared payment policy carrying the strictest numbers on the platform
/// and a comment explaining why. What it did not have was anything executing under that name:
/// before this handler, <c>financial.payment-provider</c> appeared twice in the repository, at its
/// own two declarations. Eleven payment clients set a transport timeout and nothing else.
/// </para>
/// </summary>
public sealed class ResilientHttpHandlerTests
{
    private const string Dependency = "financial.payment-provider";

    // ── The refusal ──────────────────────────────────────────────────────

    /// <summary>
    /// <b>No retry policy is not "no retry" — it is three retries.</b> The executor resolves retry
    /// by dependency name and falls back to <c>Retry.Default</c>, so a payment client wired to the
    /// breaker without declaring a policy would silently gain retries on every charge. That is the
    /// opposite of what routing it through the breaker is for, and it is the kind of default that
    /// is invisible in a diff.
    /// </summary>
    [Fact]
    public void A_dependency_with_no_retry_policy_is_refused()
    {
        var construct = () => new ResilientHttpHandler(
            new NeverCalledExecutor(), Catalog(), Dependency);

        construct.Should().Throw<InvalidOperationException>()
            .WithMessage("*declares no retry policy*")
            .WithMessage($"*{OperationalPolicyNames.Retry.Default}*");
    }

    /// <summary>
    /// A declared policy that still permits a retry is refused for the same reason, and saying so
    /// separately matters: the dangerous case is not the missing policy but the plausible one.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void A_dependency_whose_policy_permits_a_retry_is_refused(int attempts)
    {
        var construct = () => new ResilientHttpHandler(
            new NeverCalledExecutor(),
            Catalog(new RetryPolicy(Dependency) { MaxAttempts = attempts }),
            Dependency);

        construct.Should().Throw<InvalidOperationException>()
            .WithMessage($"*allowing {attempts} retries*");
    }

    [Fact]
    public void A_dependency_that_never_retries_is_accepted()
    {
        var construct = () => new ResilientHttpHandler(
            new NeverCalledExecutor(),
            Catalog(new RetryPolicy(Dependency) { MaxAttempts = 0 }),
            Dependency);

        construct.Should().NotThrow();
    }

    // ── The routing ──────────────────────────────────────────────────────

    /// <summary>
    /// <b>Every request goes through the executor, under the right name.</b> The name is the whole
    /// mechanism: it resolves the breaker, the bulkhead and the per-attempt timeout, and it labels
    /// every metric and the dashboard row. A handler that executed under the wrong name would look
    /// identical in a diff and would break the wrong provider.
    /// </summary>
    [Fact]
    public async Task A_request_runs_under_the_named_dependency()
    {
        var executor = new RecordingExecutor();
        var client = ClientWith(executor);

        var response = await client.GetAsync(new Uri("https://provider.test/charge"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        executor.Dependencies.Should().ContainSingle().Which.Should().Be(Dependency);
    }

    /// <summary>
    /// <b>A transport failure reaches the executor, which is how the breaker ever opens.</b> The
    /// failure mode the finding names is a provider that is slow rather than down: the call times
    /// out, the thread and the connection are held for the full timeout, and on a single instance
    /// enough of those take the pool. The breaker only helps if it is told.
    /// </summary>
    [Fact]
    public async Task A_transport_failure_is_seen_by_the_executor()
    {
        var executor = new RecordingExecutor();
        var client = ClientWith(executor, _ => throw new HttpRequestException("connection reset"));

        var send = async () => await client.GetAsync(new Uri("https://provider.test/charge"));

        await send.Should().ThrowAsync<HttpRequestException>();
        executor.Failures.Should().ContainSingle().Which.Should().BeOfType<HttpRequestException>();
    }

    /// <summary>
    /// <b>An open circuit refuses before the request is sent.</b> That is the point of the whole
    /// exercise — not a faster failure, but a failure that costs no connection. If the handler
    /// called first and consulted the breaker afterwards it would guard nothing.
    /// </summary>
    [Fact]
    public async Task An_open_circuit_refuses_without_calling_the_provider()
    {
        var executor = new RefusingExecutor();
        var sent = 0;
        var client = ClientWith(executor, _ => { sent++; return new HttpResponseMessage(HttpStatusCode.OK); });

        var send = async () => await client.GetAsync(new Uri("https://provider.test/charge"));

        await send.Should().ThrowAsync<InvalidOperationException>();
        sent.Should().Be(0, "an open breaker exists so the connection is never taken");
    }

    /// <summary>
    /// <b>An error response is not a failure here.</b> A 500 is the provider answering: it costs
    /// one round trip and releases the connection, which is not the failure mode this guards. The
    /// caller still sees it and decides. Worth asserting rather than leaving implied, because the
    /// obvious "improvement" is to count 5xx, and doing that under a retrying policy is how a
    /// charge gets sent twice.
    /// </summary>
    [Fact]
    public async Task An_error_response_passes_through_without_counting_as_a_failure()
    {
        var executor = new RecordingExecutor();
        var client = ClientWith(
            executor, _ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var response = await client.GetAsync(new Uri("https://provider.test/charge"));

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        executor.Failures.Should().BeEmpty();
        executor.Dependencies.Should().ContainSingle("the call was still measured");
    }

    // ── Plumbing ─────────────────────────────────────────────────────────

    private static OperationalPolicyCatalog Catalog(params OperationalPolicy[] policies) =>
        new([new StubContributor(policies)]);

    private static HttpClient ClientWith(
        IResilienceExecutor executor,
        Func<HttpRequestMessage, HttpResponseMessage>? respond = null)
    {
        var handler = new ResilientHttpHandler(
            executor, Catalog(new RetryPolicy(Dependency) { MaxAttempts = 0 }), Dependency)
        {
            InnerHandler = new StubHandler(
                respond ?? (_ => new HttpResponseMessage(HttpStatusCode.OK)))
        };

        return new HttpClient(handler);
    }

    private sealed class StubContributor : IOperationalPolicyContributor
    {
        private readonly OperationalPolicy[] _policies;

        public StubContributor(OperationalPolicy[] policies) => _policies = policies;

        public string Module => "Financial";

        public IEnumerable<OperationalPolicy> Policies => _policies;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
            _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_respond(request));
    }

    /// <summary>Runs the operation and records what it was asked to run it under.</summary>
    private sealed class RecordingExecutor : IResilienceExecutor
    {
        public List<string> Dependencies { get; } = [];

        public List<Exception> Failures { get; } = [];

        public async Task<TResult> ExecuteAsync<TResult>(
            string dependency,
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default)
        {
            Dependencies.Add(dependency);

            try
            {
                return await operation(cancellationToken);
            }
            catch (Exception exception)
            {
                Failures.Add(exception);

                throw;
            }
        }

        public Task ExecuteAsync(
            string dependency,
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default) =>
            ExecuteAsync<object?>(dependency, async token =>
            {
                await operation(token);

                return null;
            }, cancellationToken);
    }

    /// <summary>Stands in for an open circuit: refuses without running the operation.</summary>
    private sealed class RefusingExecutor : IResilienceExecutor
    {
        public Task<TResult> ExecuteAsync<TResult>(
            string dependency,
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException($"The circuit for {dependency} is open.");

        public Task ExecuteAsync(
            string dependency,
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException($"The circuit for {dependency} is open.");
    }

    private sealed class NeverCalledExecutor : IResilienceExecutor
    {
        public Task<TResult> ExecuteAsync<TResult>(
            string dependency,
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The handler should not have been constructed.");

        public Task ExecuteAsync(
            string dependency,
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The handler should not have been constructed.");
    }
}
