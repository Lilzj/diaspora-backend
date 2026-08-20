using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Diaspora.SharedKernel.Extensions;

public static class StringExtensions
{
    public static string ToSlug(this string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        var result = sb.ToString().Normalize(NormalizationForm.FormC);
        result = Regex.Replace(result, @"[^a-zA-Z0-9\s-]", "");
        result = Regex.Replace(result, @"\s+", "-");
        result = Regex.Replace(result, @"-+", "-");
        result = result.Trim('-').ToLowerInvariant();

        return result;
    }

    public static string Truncate(this string value, int maxLength, string suffix = "...")
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value;
        return string.Concat(value.AsSpan(0, maxLength - suffix.Length), suffix);
    }

    public static string Sanitize(this string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var result = Regex.Replace(value, @"<[^>]*>", string.Empty);
        result = Regex.Replace(result, @"\s+", " ");
        return result.Trim();
    }

    public static string? NullIfEmpty(this string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
