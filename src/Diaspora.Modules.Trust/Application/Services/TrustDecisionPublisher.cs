using System.Net.Http.Headers;
using System.Net.Http.Json;
using Diaspora.Modules.Trust.Domain;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Trust.Application.Services;

/// <summary>
/// Tells Identity that a member's verification outcome has changed.
/// <para>
/// ADR-002 makes this the <em>only</em> route by which a verification outcome may reach a
/// member's access state: <c>POST /api/v1/trust/decisions</c>, guarded by a permission granted
/// to no human role in any access state and reachable only with a service token. Trust owns
/// the decision; Identity owns what the decision does to a session.
/// </para>
/// <para>
/// An interface rather than a direct call, for three reasons. It keeps Trust free of any
/// Identity type; it makes the seam testable without a running host; and it is where an
/// outbox will slot in — the publisher is already the single choke point every outbound
/// decision passes through.
/// </para>
/// </summary>
public interface ITrustDecisionPublisher
{
    /// <summary>
    /// Reports an outcome. Returns false when the decision could not be delivered — the
    /// caller has already committed its own state and must not fail because Identity is
    /// briefly unreachable.
    /// </summary>
    Task<bool> PublishAsync(
        UserId userId,
        TrustDecisionOutcome outcome,
        string? reason,
        string? decidedBy,
        CancellationToken cancellationToken = default);
}

/// <summary>Where Identity's trust-decision endpoint lives, and what token opens it.</summary>
public sealed class TrustDecisionOptions
{
    public const string SectionName = "Trust:Decisions";

    /// <summary>
    /// Identity's base address. In the modular monolith this is the app's own address; when
    /// B3 is extracted it becomes Identity's. Empty disables publishing.
    /// </summary>
    public string? IdentityBaseAddress { get; set; }

    /// <summary>
    /// The service token. A distinct token type that carries no session and cannot be
    /// obtained by signing in — see ADR-002's enforcement section.
    /// </summary>
    public string? ServiceToken { get; set; }

    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>Whether publishing is configured at all.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(IdentityBaseAddress) && !string.IsNullOrWhiteSpace(ServiceToken);
}

/// <summary>
/// Posts the decision to Identity over HTTP.
/// <para>
/// An HTTP call between two modules in one process looks wasteful, and it is — deliberately.
/// ADR-002 chose it so that "B3 can be extracted into a separate service later; the contract
/// is already an HTTP call and an event". Calling Identity's MediatR command in-process would
/// be faster today and would silently re-couple the two modules, which is the cost the ADR
/// declined to pay.
/// </para>
/// <para>
/// Failures are logged and swallowed rather than thrown. The verification decision is already
/// committed in Trust's tables by the time this runs; throwing here would roll back a
/// reviewer's decision because a network hiccup, and leave the reviewer to make it again. The
/// reconciliation path for a dropped decision is the integration event, which an outbox will
/// make durable.
/// </para>
/// </summary>
public sealed class HttpTrustDecisionPublisher : ITrustDecisionPublisher
{
    /// <summary>Named client, so the host can add handlers and Polly policies to it.</summary>
    public const string HttpClientName = "trust-decisions";

    private const string Endpoint = "/api/v1/trust/decisions";

    private readonly IHttpClientFactory _clientFactory;
    private readonly IOptions<TrustDecisionOptions> _options;
    private readonly ILogger<HttpTrustDecisionPublisher> _logger;

    public HttpTrustDecisionPublisher(
        IHttpClientFactory clientFactory,
        IOptions<TrustDecisionOptions> options,
        ILogger<HttpTrustDecisionPublisher> logger)
    {
        _clientFactory = clientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<bool> PublishAsync(
        UserId userId,
        TrustDecisionOutcome outcome,
        string? reason,
        string? decidedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var options = _options.Value;

        if (!options.IsConfigured)
        {
            // Warn rather than fail. A developer running the API locally without a service
            // token should still be able to approve a verification; they simply will not see
            // the member's access state move, and the log says so plainly.
            _logger.LogWarning(
                "Trust decision '{Outcome}' for user {UserId} was not published: "
                + "Trust:Decisions is not configured.",
                outcome, userId.Value);

            return false;
        }

        try
        {
            var client = _clientFactory.CreateClient(HttpClientName);
            client.BaseAddress = new Uri(options.IdentityBaseAddress!);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", options.ServiceToken);

            // The shape is Identity's published ApplyTrustDecisionRequest. Sent as an
            // anonymous object rather than by referencing the type, so Trust depends on the
            // contract's shape and not on Identity's assembly.
            var payload = new
            {
                userId = userId.Value,
                outcome = outcome.ToString(),
                reason,
                decidedBy
            };

            using var response = await client.PostAsJsonAsync(Endpoint, payload, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Published trust decision '{Outcome}' for user {UserId}.", outcome, userId.Value);
                return true;
            }

            _logger.LogError(
                "Identity refused trust decision '{Outcome}' for user {UserId}: {StatusCode}.",
                outcome, userId.Value, (int)response.StatusCode);

            return false;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(
                exception,
                "Could not publish trust decision '{Outcome}' for user {UserId}.",
                outcome, userId.Value);

            return false;
        }
    }
}
