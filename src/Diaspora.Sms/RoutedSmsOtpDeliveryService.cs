using System.Globalization;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Application.Identity.Validation;
using Diaspora.Platform.Operations.Resilience;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace Diaspora.Sms;

/// <summary>
/// Delivers a one-time code over SMS, on whichever wire the destination routes to.
/// <para>
/// This replaces <c>LoggingOtpDeliveryService</c>, which answered <c>true</c> unconditionally and
/// logged <em>"Dispatched a code"</em> in every environment. Nothing was dispatched: the platform
/// had no SMS provider at all. The consequence was not theoretical — on the hosted deployment
/// nobody could sign in through the app, and the logs said delivery was working.
/// </para>
/// <para>
/// So the single most important property of this class is that <c>true</c> means a provider
/// accepted the message. Every path that did not reach that point returns <c>false</c>, and
/// <c>OtpChallengeService</c> already treats <c>false</c> as a failed send.
/// </para>
/// </summary>
public sealed class RoutedSmsOtpDeliveryService : IOtpDeliveryService
{
    private readonly SmsGatewayRouter _router;
    private readonly IResilienceExecutor _resilience;
    private readonly ILocalizedMessageProvider _messages;
    private readonly IClock _clock;
    private readonly ILogger<RoutedSmsOtpDeliveryService> _logger;

    public RoutedSmsOtpDeliveryService(
        SmsGatewayRouter router,
        IResilienceExecutor resilience,
        ILocalizedMessageProvider messages,
        IClock clock,
        ILogger<RoutedSmsOtpDeliveryService> logger)
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(resilience);
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(clock);

        _router = router;
        _resilience = resilience;
        _messages = messages;
        _clock = clock;
        _logger = logger;
    }

    public async Task<bool> SendAsync(
        OtpDeliveryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var route = _router.Resolve(request.PhoneNumber);

        if (route is null)
        {
            // Deliberately an error, not a warning. A deployment in this state cannot sign anybody
            // in, and that is worth waking somebody for rather than filtering past.
            _logger.LogError(
                "No SMS gateway is configured for {PhoneNumber}, so a {Purpose} code was not sent. "
                + "Check 'Sms:DefaultProvider' and 'Sms:Routes'.",
                Mask(request.PhoneNumber), request.Purpose);

            return false;
        }

        var message = new SmsMessage(request.PhoneNumber, Compose(request));

        var outcome = await TrySendAsync(route.Primary, message, request, cancellationToken)
            .ConfigureAwait(false);

        if (outcome is SendOutcome.Delivered)
        {
            return true;
        }

        // ── Only a transient failure is worth the second wire ────────────────────────────
        //
        // This distinction is the reason `TrySendAsync` does not return a bool. A refusal and an
        // outage both mean "not delivered", and collapsing them sends every invalid number to a
        // second provider to be rejected a second time — at the second provider's price, and with
        // the member still told nothing useful about their number.
        if (outcome is SendOutcome.Refused || route.Fallback is null)
        {
            return false;
        }

        _logger.LogWarning(
            "Falling back to {Fallback} for a {Purpose} code after {Primary} could not carry it.",
            route.Fallback.Provider, request.Purpose, route.Primary.Provider);

        return await TrySendAsync(route.Fallback, message, request, cancellationToken)
            .ConfigureAwait(false) is SendOutcome.Delivered;
    }

    /// <summary>
    /// What one gateway did with a message.
    /// <para>
    /// Three states rather than two, because the caller's next move differs for each:
    /// <see cref="Refused"/> ends the attempt entirely, while <see cref="Unavailable"/> is exactly
    /// the case a second provider exists for.
    /// </para>
    /// </summary>
    private enum SendOutcome
    {
        /// <summary>A provider accepted it.</summary>
        Delivered,

        /// <summary>The provider looked at it and said no. Another provider will say no too.</summary>
        Refused,

        /// <summary>The provider could not be reached, or failed and kept failing.</summary>
        Unavailable
    }

    /// <summary>
    /// One gateway, under the platform's retry, breaker and bulkhead.
    /// <para>
    /// Returning an outcome rather than rethrowing is what lets the caller try the other wire.
    /// The distinction that matters is drawn by the executor: a
    /// <see cref="SmsPermanentException"/> is classified permanent, so it is never retried here —
    /// and it comes back as <see cref="SendOutcome.Refused"/>, which stops the fallback too.
    /// </para>
    /// </summary>
    private async Task<SendOutcome> TrySendAsync(
        ISmsGateway gateway,
        SmsMessage message,
        OtpDeliveryRequest request,
        CancellationToken cancellationToken)
    {
        var dependency = SmsDependencies.For(gateway.Provider);

        try
        {
            var dispatch = await _resilience.ExecuteAsync(
                dependency,
                token => gateway.SendAsync(message, token),
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "A {Purpose} code was accepted by {Provider} for {PhoneNumber} ({MessageId}).",
                request.Purpose,
                dispatch.Provider,
                Mask(request.PhoneNumber),
                dispatch.ProviderMessageId ?? "no id");

            return SendOutcome.Delivered;
        }
        catch (SmsPermanentException exception)
        {
            // Not retried and not worth a second provider. An unroutable number is unroutable on
            // both wires, and a member whose number is wrong needs to be told, not routed around.
            _logger.LogError(
                "{Provider} will not carry a {Purpose} code to {PhoneNumber}: {Reason}",
                gateway.Provider, request.Purpose, Mask(request.PhoneNumber), exception.Message);

            return SendOutcome.Refused;
        }
        catch (CircuitOpenException exception)
        {
            // The provider was not called at all, which is exactly the case a fallback exists for.
            _logger.LogWarning(
                "The circuit for {Provider} is open until {ClosesAt:O}; it was not called.",
                gateway.Provider, exception.ClosesAt);

            return SendOutcome.Unavailable;
        }
        catch (BulkheadFullException)
        {
            _logger.LogWarning(
                "{Provider} is at its concurrency limit; the message was not attempted.",
                gateway.Provider);

            return SendOutcome.Unavailable;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "{Provider} did not answer within the policy's timeout.", gateway.Provider);

            return SendOutcome.Unavailable;
        }
        catch (SmsTransientException exception)
        {
            // The retries the policy allows are already spent by the time this surfaces.
            _logger.LogWarning(
                exception,
                "{Provider} could not carry a {Purpose} code after retrying.",
                gateway.Provider, request.Purpose);

            return SendOutcome.Unavailable;
        }
    }

    /// <summary>
    /// The message body, in the member's language.
    /// <para>
    /// The expiry is rendered in whole minutes rather than as a timestamp: a member reading an SMS
    /// wants to know whether the code is still good, not when it was issued, and a UTC instant in
    /// an SMS is a support ticket.
    /// </para>
    /// </summary>
    private string Compose(OtpDeliveryRequest request)
    {
        var remaining = request.ExpiresAt - _clock.UtcNow;

        // At least one, always. A code that expires in forty seconds reads as "0 minutes", which
        // looks like an already-dead code and sends the member straight to Resend.
        var minutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));

        var template = _messages.GetFor(IdentityMessageKeys.OtpSmsBody, request.Language);

        try
        {
            return string.Format(CultureInfo.InvariantCulture, template, request.Code, minutes);
        }
        catch (FormatException)
        {
            // A translation with a malformed placeholder must not cost the member their code.
            // The digits alone still work; the sentence around them is what is lost.
            _logger.LogError(
                "The OTP SMS template for language '{Language}' has a malformed placeholder.",
                request.Language);

            return request.Code;
        }
    }

    /// <summary>
    /// Enough of the number to match against a support conversation, not enough to be a contact
    /// record in a log aggregator.
    /// </summary>
    private static string Mask(string phoneNumber) =>
        phoneNumber.Length <= 6
            ? "***"
            : $"{phoneNumber[..3]}***{phoneNumber[^3..]}";
}
