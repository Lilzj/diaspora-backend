using Diaspora.Platform.Operations.Policies;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.Platform.Operations.Resilience;

/// <summary>
/// Puts a named client under a dependency's policies, in one line at the registration.
/// </summary>
public static class ResilientHttpClientBuilderExtensions
{
    /// <summary>
    /// Routes every request this client makes through <see cref="IResilienceExecutor"/>.
    /// <para>
    /// The handler is constructed when the client is first created rather than at start-up, so the
    /// refusal it may throw arrives on the first call through that client. That is late, which is
    /// why an architecture rule asserts the same thing about the catalogue at build time; this is
    /// the backstop for a policy changed after the build.
    /// </para>
    /// </summary>
    public static IHttpClientBuilder WithPlatformResilience(
        this IHttpClientBuilder builder, string dependency)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddHttpMessageHandler(provider => new ResilientHttpHandler(
            provider.GetRequiredService<IResilienceExecutor>(),
            provider.GetRequiredService<IOperationalPolicyCatalog>(),
            dependency));
    }
}

/// <summary>
/// Puts an outbound <see cref="HttpClient"/> under the platform's resilience policies.
/// <para>
/// <b>The platform had the breaker and nothing to break.</b>
/// <c>OperationalPolicyNames.CircuitBreaker</c>'s payment breaker has been declared since B17 with the
/// strictest settings on the platform and a comment explaining exactly why — <em>duplicate charges
/// are created by retries against a provider that is timing out after it has already taken the
/// money</em>. It is in the policy catalogue, on the operator dashboard, in the health contributor
/// and in the isolate/reset controls. Before this handler, the name appeared in exactly two places
/// in the repository: where the constant is declared and where the policy is declared. Not one
/// payment call ran under it.
/// </para>
/// <para>
/// <b>Registered on the client rather than written into each gateway.</b> The three payment
/// adapters hold eleven named clients between them and each gateway makes its own calls; wrapping
/// call sites would mean finding all of them and would miss the twelfth. A handler is one
/// registration per client and it governs every request that client will ever make, which is what
/// the finding asked for — <em>register it the same way the platform registers everything else,
/// once, centrally, so a new adapter inherits it</em>.
/// </para>
/// <para>
/// <b>What this does not do is turn an error response into a failure.</b> A 500 from a provider is
/// the provider answering, and it costs the platform nothing to receive; this guards the failure
/// mode the finding names, which is a provider that is <em>slow</em> — every request holding a
/// thread and a connection for the full timeout until the pool is gone. Those surface as exceptions
/// and the breaker counts them. Whichever timeout fires first does the right thing: the executor
/// re-throws its own attempt timeout as a <c>TimeoutException</c> specifically so the breaker sees
/// it, and an <see cref="HttpClient"/> timeout arrives as an <see cref="OperationCanceledException"/>
/// the caller did not request, which that same code converts.
/// </para>
/// </summary>
public sealed class ResilientHttpHandler : DelegatingHandler
{
    private readonly IResilienceExecutor _executor;
    private readonly string _dependency;

    /// <summary>
    /// Binds a client to a dependency name.
    /// </summary>
    /// <param name="policies">
    /// Read once, to refuse a configuration that would retry. See the exception below — it is the
    /// whole reason this takes a catalogue it otherwise has no use for.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// <b>When <paramref name="dependency"/> has no retry policy, or one that permits a retry.</b>
    /// Two separate reasons, either sufficient. A retried <c>POST</c> to a payment provider is the
    /// duplicate charge the breaker's own documentation is about — the executor cannot know a call
    /// is non-idempotent, so the policy has to say so. And the executor's retry re-invokes the
    /// operation, which here re-sends one <see cref="HttpRequestMessage"/>; a request whose content
    /// has been consumed is not resendable, so the second attempt fails for a reason that has
    /// nothing to do with the provider and reports as though the provider failed twice.
    /// <para>
    /// Refused loudly rather than defaulted quietly, because the default is the trap:
    /// <c>RetryFor</c> falls back to <c>Retry.Default</c>, which is three attempts. Wiring a
    /// payment client to this handler without declaring a policy would silently add two retries to
    /// every charge — the opposite of what routing it through the breaker is for.
    /// </para>
    /// </exception>
    public ResilientHttpHandler(
        IResilienceExecutor executor, IOperationalPolicyCatalog policies, string dependency)
    {
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentException.ThrowIfNullOrWhiteSpace(dependency);

        var retry = policies.Find<RetryPolicy>(dependency);

        if (retry is null || retry.MaxAttempts > 0)
        {
            throw new InvalidOperationException(
                $"'{dependency}' is wired to {nameof(ResilientHttpHandler)} and " +
                (retry is null
                    ? "declares no retry policy, so the executor would fall back to " +
                      $"'{OperationalPolicyNames.Retry.Default}' and retry it."
                    : $"declares a retry policy allowing {retry.MaxAttempts} retries.") +
                " An outbound HTTP request is sent from a single HttpRequestMessage and may carry " +
                "a non-idempotent operation, so a client governed by this handler must declare a " +
                "retry policy of MaxAttempts = 0. Retry belongs above it, where the caller can " +
                "build a fresh request and knows whether repeating the call is safe.");
        }

        _executor = executor;
        _dependency = dependency;
    }

    /// <summary>The dependency every request through this handler is measured and broken on.</summary>
    public string Dependency => _dependency;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        _executor.ExecuteAsync(
            _dependency,
            token => base.SendAsync(request, token),
            cancellationToken);
}
