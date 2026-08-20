using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Domain.ValueObjects;

/// <summary>
/// A normalised file extension, without the dot, lower-cased.
/// <para>
/// A value object rather than a string because the normalisation has to happen in exactly one
/// place. <c>.JPG</c>, <c>jpg</c> and <c>.jpg</c> are one extension; an allow-list checked
/// against un-normalised input is an allow-list with holes, and case-folding at each call site
/// is how one of them ends up missing.
/// </para>
/// <para>
/// Only the final segment counts. <c>photo.jpg.exe</c> has extension <c>exe</c>, which is the
/// whole point of parsing it rather than searching the name for a known-good substring.
/// </para>
/// </summary>
public sealed class FileExtension : ValueObject
{
    public const int MaxLength = 16;

    /// <summary>A file with no extension at all. Legal; simply carries no format claim.</summary>
    public static readonly FileExtension None = new(string.Empty);

    private FileExtension(string value)
    {
        Value = value;
    }

    /// <summary>The extension without its dot, lower-cased. Empty when there is none.</summary>
    public string Value { get; }

    public bool HasValue => Value.Length > 0;

    /// <summary>Reads the extension off a file name.</summary>
    public static FileExtension FromFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return None;
        }

        var lastDot = fileName.LastIndexOf('.');

        if (lastDot < 0 || lastDot == fileName.Length - 1)
        {
            return None;
        }

        return TryCreate(fileName[(lastDot + 1)..]) is { IsSuccess: true } result
            ? result.Value
            : None;
    }

    public static Result<FileExtension> TryCreate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Success(None);
        }

        var normalized = value.Trim().TrimStart('.').ToLowerInvariant();

        if (normalized.Length is 0 or > MaxLength || !normalized.All(char.IsAsciiLetterOrDigit))
        {
            return Result.Failure<FileExtension>(Error.Validation(
                MediaErrorCodes.ExtensionNotAllowed,
                "A file extension may only contain letters and digits."));
        }

        return Result.Success(new FileExtension(normalized));
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
