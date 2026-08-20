using System.Globalization;
using System.Text;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Domain.ValueObjects;

/// <summary>
/// A file name that is safe to put in a storage key, a <c>Content-Disposition</c> header and a
/// log line.
/// <para>
/// The original name a client sends is attacker-controlled in every deployment: it can contain
/// <c>../</c>, a null byte, a right-to-left override that makes <c>gpj.exe</c> render as
/// <c>exe.jpg</c>, a Windows device name, or four kilobytes of nothing. This type is where all
/// of that stops.
/// </para>
/// <para>
/// Both forms are kept. <see cref="Original"/> is what the member typed and is shown back to
/// them — a member who uploaded <c>Passeport – Côte d'Ivoire.pdf</c> should see that name.
/// <see cref="Value"/> is the sanitised form, and it is the only one that ever reaches a
/// filesystem, a key or a header.
/// </para>
/// </summary>
public sealed class SafeFileName : ValueObject
{
    public const int MaxLength = 200;

    /// <summary>What an upload with no usable name is called.</summary>
    public const string Fallback = "file";

    /// <summary>
    /// Reserved device names on Windows. A file called <c>CON</c> or <c>LPT1</c> is not
    /// creatable there, and a deployment that runs the filesystem provider on Windows would
    /// fail on it in a way that looks like a storage outage.
    /// </summary>
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    private SafeFileName(string value, string original, FileExtension extension)
    {
        Value = value;
        Original = original;
        Extension = extension;
    }

    /// <summary>The sanitised name. Safe in a key, a path and a header.</summary>
    public string Value { get; }

    /// <summary>What the client sent, trimmed and length-capped. Display only.</summary>
    public string Original { get; }

    public FileExtension Extension { get; }

    /// <summary>
    /// Sanitises a client-supplied name. Never fails: a name that survives nothing becomes
    /// <see cref="Fallback"/> plus its extension, because refusing an upload over its name is
    /// a worse outcome than renaming it.
    /// </summary>
    public static SafeFileName Create(string? fileName)
    {
        var original = Truncate(fileName?.Trim() ?? string.Empty, MaxLength);
        var extension = FileExtension.FromFileName(original);

        var stem = StripExtension(original, extension);
        var sanitized = Sanitize(stem);

        if (sanitized.Length == 0 || ReservedNames.Contains(sanitized))
        {
            sanitized = Fallback;
        }

        var value = extension.HasValue ? $"{sanitized}.{extension.Value}" : sanitized;

        return new SafeFileName(value, original.Length == 0 ? value : original, extension);
    }

    /// <summary>
    /// Re-bases the name on a new extension, for a derived rendition. The stem is kept so a
    /// downloaded thumbnail is still recognisably the file the member uploaded.
    /// </summary>
    public SafeFileName WithExtension(FileExtension extension)
    {
        ArgumentNullException.ThrowIfNull(extension);

        var stem = StripExtension(Value, Extension);
        return Create(extension.HasValue ? $"{stem}.{extension.Value}" : stem);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <summary>
    /// Keeps letters, digits, and three separators; folds everything else to a hyphen.
    /// <para>
    /// An allow-list rather than a deny-list, and unicode-category-based rather than
    /// ASCII-only, so <c>Côte</c> survives while the bidirectional override characters that
    /// make an extension render backwards do not — they are <c>Cf</c> (format) characters and
    /// are not letters.
    /// </para>
    /// </summary>
    private static string Sanitize(string value)
    {
        var builder = new StringBuilder(value.Length);
        var lastWasSeparator = false;

        foreach (var character in value.Normalize(NormalizationForm.FormC))
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);

            var keep = char.IsLetterOrDigit(character) &&
                       category is not (UnicodeCategory.Format or
                                        UnicodeCategory.Control or
                                        UnicodeCategory.Surrogate or
                                        UnicodeCategory.PrivateUse);

            if (keep)
            {
                builder.Append(character);
                lastWasSeparator = false;
                continue;
            }

            if (character is ' ' or '-' or '_' or '.')
            {
                if (!lastWasSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                    lastWasSeparator = true;
                }
            }
        }

        return builder.ToString().Trim('-');
    }

    private static string StripExtension(string value, FileExtension extension)
    {
        if (!extension.HasValue)
        {
            return value;
        }

        var suffix = $".{extension.Value}";

        return value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? value[..^suffix.Length]
            : value;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
