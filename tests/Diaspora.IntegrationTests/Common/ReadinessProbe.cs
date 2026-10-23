using System.Text.Json;

namespace Diaspora.IntegrationTests.Common;

/// <summary>
/// Reads one named check from <c>/health/ready</c>, past this suite's shared-connection artefact and
/// no further.
/// <para>
/// <b>The artefact.</b> Readiness runs its checks concurrently — <c>database</c>, <c>modules</c>,
/// <c>outbox</c>, <c>transactions</c>, the module adapter — and in this suite's host every one of
/// them queries the same in-memory <c>SqliteConnection</c>. Under load (a full run, every collection
/// in parallel) the overlap occasionally makes a check throw. ASP.NET reports a check that threw
/// with its registered failure status, an <c>error</c>, and <c>data: null</c>: the outbox check
/// then reads <c>Unhealthy</c> and the transactions check loses its data, which is how
/// <c>Readiness_reports_the_outbox</c> and
/// <c>A_non_retrying_provider_writes_no_verification_receipts</c> each failed now and then while
/// passing every time in isolation. Production gives each check its own pooled connection; nothing
/// here is a product fault.
/// </para>
/// <para>
/// <b>What this retries, and what it never does.</b> Only an entry whose check <em>threw</em> — it
/// carries an <c>error</c> — is read again, a few times. An entry the check actually evaluated is
/// returned as it is, whatever its status, so a genuinely Unhealthy outbox still fails its test on
/// the first read. A check that throws on every attempt fails the test with its own error text.
/// </para>
/// </summary>
public static class ReadinessProbe
{
    private const int Attempts = 5;

    /// <summary>The named check's entry, as evaluated rather than as thrown.</summary>
    public static async Task<JsonElement> ReadinessCheckAsync(this HttpClient client, string name)
    {
        ArgumentNullException.ThrowIfNull(client);

        var thrown = string.Empty;

        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            var entry = document.RootElement.GetProperty("checks")
                .EnumerateArray()
                .Single(check => check.GetProperty("name").GetString() == name);

            if (!Threw(entry))
            {
                // Cloned: the element must outlive the document it came from.
                return entry.Clone();
            }

            thrown = entry.GetRawText();
            await Task.Delay(TimeSpan.FromMilliseconds(50 * attempt));
        }

        throw new InvalidOperationException(
            $"The '{name}' readiness check threw on all {Attempts} attempts, so it was never " +
            $"evaluated. Last entry: {thrown}");
    }

    private static bool Threw(JsonElement entry) =>
        entry.TryGetProperty("error", out var error)
        && error.ValueKind == JsonValueKind.String
        && !string.IsNullOrEmpty(error.GetString());
}
