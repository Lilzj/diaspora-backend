using System.Text;

namespace Diaspora.Modules.Notifications.Domain.Templates;

/// <summary>
/// Finds the <c>{token}</c> holes in a piece of template text.
/// <para>
/// <b>A hand-written scanner rather than a regular expression, and rather than a templating
/// library.</b> A library would be a dependency taken so that reference data could contain
/// expressions — which is to say, so that reference data could contain code. This module
/// deliberately supports substitution and nothing else: no conditionals, no loops, no property
/// paths. An operator editing a template cannot write anything that can fail at render time except
/// a name that does not exist, and publication catches that.
/// </para>
/// <para>
/// A regular expression would do the same job in one line and would also backtrack unboundedly on
/// pathological input, which is a denial-of-service vector in a code path that runs once per
/// notification per channel.
/// </para>
/// </summary>
public static class TemplateTokenScanner
{
    /// <summary>
    /// Every distinct token name, lower-cased. Unclosed braces are ignored rather than reported —
    /// a template with a stray brace renders it literally, which is visible and harmless.
    /// </summary>
    public static IReadOnlyList<string> Scan(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var tokens = new List<string>();
        var buffer = new StringBuilder();
        var inToken = false;

        foreach (var character in text)
        {
            if (character == '{')
            {
                // A second opening brace abandons whatever was being read. "{a{b}" yields "b".
                inToken = true;
                buffer.Clear();
                continue;
            }

            if (character == '}')
            {
                if (inToken && buffer.Length > 0)
                {
                    var token = buffer.ToString().Trim().ToLowerInvariant();

                    if (token.Length > 0 && !tokens.Contains(token, StringComparer.Ordinal))
                    {
                        tokens.Add(token);
                    }
                }

                inToken = false;
                buffer.Clear();
                continue;
            }

            if (inToken)
            {
                buffer.Append(character);
            }
        }

        return tokens;
    }
}
