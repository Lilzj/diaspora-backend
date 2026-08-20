using System.Globalization;

namespace Diaspora.Api.Hosting;

/// <summary>
/// The container's health check, run in-process.
/// <para>
/// The Dockerfile used to probe with <c>curl</c>, which the ASP.NET runtime image does not
/// contain — so the health check could only ever report unhealthy, and an orchestrator
/// configured to act on it would have restarted every container in a loop. Rather than
/// installing a package into the runtime image to make one HTTP call, the app makes it.
/// </para>
/// <para>
/// Deliberately hits liveness, not readiness. A container whose database is briefly
/// unreachable is not a container that should be killed and replaced.
/// </para>
/// </summary>
public static class ContainerHealthProbe
{
    private const string Flag = "--health-check";

    public static bool IsRequested(string[] args) =>
        args.Contains(Flag, StringComparer.Ordinal);

    public static async Task<int> RunAsync()
    {
        var port = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORT")
            ?? ExtractPort(Environment.GetEnvironmentVariable("ASPNETCORE_URLS"))
            ?? "8080";

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };

        try
        {
            var response = await client.GetAsync(
                new Uri($"http://localhost:{port}/health/live"));

            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            await Console.Error.WriteLineAsync(
                string.Create(CultureInfo.InvariantCulture, $"Health probe failed: {exception.Message}"));

            return 1;
        }
    }

    private static string? ExtractPort(string? urls)
    {
        if (string.IsNullOrWhiteSpace(urls))
        {
            return null;
        }

        var first = urls.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

        return Uri.TryCreate(first, UriKind.Absolute, out var uri)
            ? uri.Port.ToString(CultureInfo.InvariantCulture)
            : null;
    }
}
