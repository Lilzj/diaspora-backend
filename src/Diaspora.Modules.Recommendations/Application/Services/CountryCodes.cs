namespace Diaspora.Modules.Recommendations.Application.Services;

/// <summary>The shape check every country parameter on this module's own handlers shares.</summary>
public static class CountryCodes
{
    /// <summary>Two ASCII letters, either case. Whether the country exists is not this check's question.</summary>
    public static bool IsWellFormed(string? value)
    {
        var trimmed = value?.Trim();

        return trimmed is { Length: 2 } && char.IsAsciiLetter(trimmed[0]) && char.IsAsciiLetter(trimmed[1]);
    }

    /// <summary>Upper-cased, or null when absent or malformed.</summary>
    public static string? Normalise(string? value) =>
        IsWellFormed(value) ? value!.Trim().ToUpperInvariant() : null;
}
