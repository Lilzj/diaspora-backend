using System.Text;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Application.Mapping;

/// <summary>
/// Translates between the domain's enums and the camel-cased strings on the wire.
/// <para>
/// <b>Why strings and not integers.</b> An enum serialised as a number is a contract nobody
/// can read, that breaks silently when a value is inserted in the middle, and that a client
/// developer has to keep a lookup table for. <c>"quarantined"</c> is self-describing in a log,
/// in a network tab and in a bug report; <c>3</c> is not.
/// </para>
/// <para>
/// <b>Why a single translation point.</b> These strings are frozen contract — a client
/// branching on <c>uploadState === 'paused'</c> must keep working across releases — and a
/// codebase that camel-cases enum names inline at each of forty call sites has forty chances
/// to produce <c>"partiallySucceeded"</c> in one place and <c>"partially_succeeded"</c> in
/// another. One converter, exercised by the contract tests.
/// </para>
/// </summary>
public static class MediaWireNames
{
    /// <summary>An enum value as the client sees it: <c>PartiallySucceeded</c> → <c>partiallySucceeded</c>.</summary>
    public static string ToWire<TEnum>(TEnum value) where TEnum : struct, Enum =>
        Camel(value.ToString() ?? string.Empty);

    /// <summary>
    /// Parses a wire name back to its enum value, accepting any casing.
    /// <para>
    /// Case-insensitive because clients differ: one sends <c>"providerGallery"</c>, another
    /// <c>"ProviderGallery"</c>, and refusing the second buys nothing but support tickets.
    /// Unknown values are refused rather than defaulting — a typo silently becoming
    /// <c>Unknown</c> is how an upload ends up with the wrong size ceiling.
    /// </para>
    /// </summary>
    public static Result<TEnum> Parse<TEnum>(string? value, Error error) where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(error);

        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<TEnum>(error);
        }

        return Enum.TryParse<TEnum>(value.Trim(), ignoreCase: true, out var parsed) &&
               Enum.IsDefined(parsed)
            ? Result.Success(parsed)
            : Result.Failure<TEnum>(error);
    }

    /// <summary>
    /// Parses a wire name, falling back when it is absent. Used where the field is genuinely
    /// optional — a variant defaulting to <c>original</c>, an owner type defaulting to
    /// unassigned.
    /// </summary>
    public static TEnum ParseOrDefault<TEnum>(string? value, TEnum fallback)
        where TEnum : struct, Enum =>
        !string.IsNullOrWhiteSpace(value) &&
        Enum.TryParse<TEnum>(value.Trim(), ignoreCase: true, out var parsed) &&
        Enum.IsDefined(parsed)
            ? parsed
            : fallback;

    private static string Camel(string pascal)
    {
        if (pascal.Length == 0)
        {
            return pascal;
        }

        var builder = new StringBuilder(pascal.Length);

        builder.Append(char.ToLowerInvariant(pascal[0]));
        builder.Append(pascal.AsSpan(1));

        return builder.ToString();
    }
}
