namespace Diaspora.Api.Middleware;

/// <summary>
/// The host names this deployment answers on (BSEC-012), read from ASP.NET Core's own
/// <c>AllowedHosts</c> key: names separated by <c>;</c>, with no scheme and no port.
/// <para>
/// Read here rather than left to the framework, which would apply the list to every request, the
/// orchestrator's probes included. <see cref="AllowedHostFiltering"/> says why that matters on
/// Fly.io and applies the list to everything else.
/// </para>
/// </summary>
public sealed class AllowedHostOptions
{
    /// <summary>The configuration key, shared with ASP.NET Core's own host filtering.</summary>
    public const string ConfigurationKey = "AllowedHosts";

    /// <summary>The configured names, in order. Empty when the key is unset.</summary>
    public IReadOnlyList<string> Hosts { get; set; } = [];

    /// <summary>Whether the list accepts any Host header: unset, or holding a bare <c>*</c>.</summary>
    public bool AcceptsAnyHost => Hosts.Count == 0 || Hosts.Contains("*", StringComparer.Ordinal);

    /// <summary>Splits a configured value as ASP.NET Core does, on <c>;</c>, dropping blank entries.</summary>
    public static IReadOnlyList<string> Parse(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
