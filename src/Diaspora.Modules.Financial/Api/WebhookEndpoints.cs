using System.Text;
using Diaspora.Modules.Financial.Application.Webhooks;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Financial.Api;

/// <summary>
/// Where payment providers tell the platform what actually happened.
/// <para>
/// <b>The only anonymous route this module has, and it is anonymous because the signature is the
/// credential.</b> A provider holds no bearer token; it holds a shared secret and signs the body with
/// it. The registered processor verifies that before a field of the payload is read, and a callback
/// that fails is refused without being believed.
/// </para>
/// <para>
/// <b>The raw bytes are read once and passed through unaltered.</b> Model binding would deserialise
/// and re-serialise the payload, and a signature verified over a re-serialised object is a signature
/// verified over something the provider never sent — an integration that works until a property order
/// changes. The body is read as text, capped, and handed to the adapter exactly as it arrived.
/// </para>
/// <para>
/// <b>Every response is deliberate.</b> 400 for a signature that did not verify — loud, because that
/// is either a rotated secret nobody updated or somebody probing. 404 for a provider with no
/// registered processor. 200 for everything else, <em>including</em> a callback about a payment this
/// deployment has never heard of: answering 4xx there buys an unbounded redelivery loop against a
/// reference that will never resolve.
/// </para>
/// </summary>
internal static class WebhookEndpoints
{
    /// <summary>
    /// The largest callback body the platform will read, in bytes.
    /// <para>
    /// A megabyte is far above any real provider payload and far below what an unauthenticated
    /// caller could use to exhaust memory on a route that, by design, anybody can reach.
    /// </para>
    /// </summary>
    private const int MaxBodyBytes = 1024 * 1024;

    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/webhooks/{provider}", async (
                string provider,
                HttpContext context,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var body = await ReadBodyAsync(context.Request, cancellationToken);

                if (body is null)
                {
                    return Microsoft.AspNetCore.Http.Results.Problem(
                        PlatformProblem.Create(
                            context,
                            StatusCodes.Status413PayloadTooLarge,
                            FinancialErrorCodes.WebhookPayloadTooLarge,
                            "Payload too large",
                            "That callback body is larger than this platform will read."));
                }

                return PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ProcessPaymentWebhookCommand
                        {
                            Provider = provider,
                            RawBody = body,
                            Headers = Headers(context.Request)
                        },
                        cancellationToken));
            })
            .AllowAnonymous()
            .WithName("ProcessPaymentWebhook")
            .WithSummary(
                "A payment provider's callback. Signature-verified, replay-protected, and the only "
                + "thing on this platform that may confirm a payment on a provider's word.");
    }

    /// <summary>
    /// Exactly the bytes that arrived, as text — or null when there are too many of them.
    /// <para>
    /// Read into a bounded buffer rather than with <c>ReadToEndAsync</c>: an unauthenticated caller
    /// choosing how much memory the server allocates is the whole of that vulnerability, and a
    /// content-length header is a claim rather than a fact.
    /// </para>
    /// </summary>
    private static async Task<string?> ReadBodyAsync(
        HttpRequest request, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        using var accumulated = new MemoryStream();

        while (true)
        {
            var read = await request.Body.ReadAsync(buffer, cancellationToken);

            if (read == 0)
            {
                break;
            }

            if (accumulated.Length + read > MaxBodyBytes)
            {
                return null;
            }

            accumulated.Write(buffer, 0, read);
        }

        return Encoding.UTF8.GetString(accumulated.ToArray());
    }

    /// <summary>
    /// The request headers, flattened.
    /// <para>
    /// Which header carries the signature is the provider's decision and therefore the adapter's
    /// knowledge, so all of them travel and the adapter picks. A repeated header is joined rather
    /// than dropped — a signature split across two values would otherwise verify against half of
    /// itself.
    /// </para>
    /// </summary>
    private static Dictionary<string, string> Headers(HttpRequest request)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in request.Headers)
        {
            headers[header.Key] = string.Join(',', header.Value.ToArray());
        }

        return headers;
    }
}
