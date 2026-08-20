using Microsoft.Extensions.Options;

namespace Diaspora.Api.Middleware;

/// <summary>
/// Refuses to start on a prefix that cannot be a path.
/// <para>
/// <c>PathString</c> throws on a value with no leading slash, and it would throw while the
/// middleware pipeline was being built — an <c>ArgumentException</c> naming a parameter, from a
/// frame with no mention of the setting that caused it. A deployment that typed <c>api</c> for
/// <c>/api</c> deserves to be told which key is wrong, before the host claims to be up.
/// </para>
/// <para>
/// A trailing slash is refused for the same reason it would be wrong rather than harmless:
/// <c>"/api/"</c> prepended to <c>"/v1/conversations"</c> is <c>"/api//v1/conversations"</c>, which
/// routes to nothing and looks like the middleware not running.
/// </para>
/// </summary>
public sealed class ApiRoutingOptionsValidator : IValidateOptions<ApiRoutingOptions>
{
    public ValidateOptionsResult Validate(string? name, ApiRoutingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        Check(options.RootPrefix, $"{ApiRoutingOptions.SectionName}:RootPrefix", failures);

        for (var index = 0; index < options.VersionSegments.Length; index++)
        {
            Check(
                options.VersionSegments[index],
                $"{ApiRoutingOptions.SectionName}:VersionSegments[{index}]",
                failures);
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void Check(string? value, string key, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add($"'{key}' must be a path segment such as '/api'. It was empty.");
            return;
        }

        if (!value.StartsWith('/'))
        {
            failures.Add($"'{key}' must begin with '/'. It was '{value}'.");
        }

        if (value.Length > 1 && value.EndsWith('/'))
        {
            failures.Add(
                $"'{key}' must not end with '/', or the rewritten path carries an empty segment. "
                + $"It was '{value}'.");
        }
    }
}
