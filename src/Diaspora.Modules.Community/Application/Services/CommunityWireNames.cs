using System.Text;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Services;

/// <summary>
/// Translates between enum members and their wire form.
/// <para>
/// Two jobs, and the second is the one that matters. Emitting camelCase is the platform rule
/// B3, B4 and B5 all follow. <b>Accepting the client's spellings</b> is what stops this phase
/// breaking a client that has three different casing conventions and two names for the same
/// concept — its content types are PascalCase, its review flags are snake_case, its report
/// modal is lower-case, and it calls a post a <c>CommunityUpdate</c>.
/// </para>
/// <para>
/// The alias table is therefore not a convenience: it is the frontend contract report's naming
/// resolution, in code, in one place, where a test can assert every entry.
/// </para>
/// </summary>
public static class CommunityWireNames
{
    private static readonly Dictionary<string, CommunityContentType> ContentTypeAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["communityupdate"] = CommunityContentType.Post,
            ["update"] = CommunityContentType.Post,
            ["communitypost"] = CommunityContentType.Post,
            ["officialnotice"] = CommunityContentType.Notice,
            ["provider"] = CommunityContentType.Listing,
        };

    private static readonly Dictionary<string, ReportReasonCode> ReasonAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // The five keys the client's report modal actually submits, mapped onto the eight
            // codes its moderation types declare. The modal is what renders; the codes are what
            // a queue can act on.
            ["conflicting"] = ReportReasonCode.Misinformation,
            ["inaccurate"] = ReportReasonCode.Outdated,
            ["abusive"] = ReportReasonCode.Harassment,
            ["personaldata"] = ReportReasonCode.PersonalData,
            ["personal_data"] = ReportReasonCode.PersonalData,
        };

    /// <summary>The wire form of an enum member: the name, camelCased.</summary>
    public static string ToWire<TEnum>(TEnum value) where TEnum : struct, Enum =>
        Camel(value.ToString()!);

    /// <summary>
    /// Parses an enum member from any spelling a client might send.
    /// <para>
    /// Underscores and hyphens are stripped before matching, which is what makes
    /// <c>hate_speech</c>, <c>HateSpeech</c> and <c>hate-speech</c> all resolve.
    /// </para>
    /// </summary>
    public static Result<TEnum> Parse<TEnum>(string? value, string name) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<TEnum>(CommunityErrors.EnumUnknown(name, "(empty)"));
        }

        var cleaned = value.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Trim();

        return Enum.TryParse<TEnum>(cleaned, ignoreCase: true, out var parsed)
            ? Result.Success(parsed)
            : Result.Failure<TEnum>(CommunityErrors.EnumUnknown(name, value));
    }

    public static TEnum ParseOr<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum
    {
        var parsed = Parse<TEnum>(value, typeof(TEnum).Name);

        return parsed.IsSuccess ? parsed.Value : fallback;
    }

    /// <summary>Parses a content type, honouring the client's own names for the same things.</summary>
    public static Result<CommunityContentType> ParseContentType(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            ContentTypeAliases.TryGetValue(value.Trim(), out var aliased))
        {
            return Result.Success(aliased);
        }

        return Parse<CommunityContentType>(value, "content type");
    }

    /// <summary>Parses a report reason, honouring the report modal's five lower-case keys.</summary>
    public static Result<ReportReasonCode> ParseReportReason(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && ReasonAliases.TryGetValue(value.Trim(), out var aliased))
        {
            return Result.Success(aliased);
        }

        return Parse<ReportReasonCode>(value, "report reason");
    }

    private static string Camel(string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);
        builder.Append(char.ToLowerInvariant(value[0]));

        for (var index = 1; index < value.Length; index++)
        {
            builder.Append(value[index]);
        }

        return builder.ToString();
    }

    /// <summary>Every member of an enum, in wire form. Used by the capabilities route.</summary>
    public static IReadOnlyList<string> All<TEnum>() where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>().Select(ToWire).ToList();

    /// <summary>A human label for a reason code, matching the client's own label map.</summary>
    public static string ReasonLabel(ReportReasonCode reason) => reason switch
    {
        ReportReasonCode.Misinformation => "Misinformation",
        ReportReasonCode.Spam => "Spam",
        ReportReasonCode.Harassment => "Harassment",
        ReportReasonCode.Inappropriate => "Inappropriate Content",
        ReportReasonCode.Outdated => "Outdated Information",
        ReportReasonCode.Copyright => "Copyright Violation",
        ReportReasonCode.PersonalData => "Exposes Personal Data",
        _ => "Other"
    };

    /// <summary>A human label for a content type. Used on the saved screen's tiles.</summary>
    public static string ContentTypeLabel(CommunityContentType type) => type switch
    {
        CommunityContentType.Post => "Updates",
        CommunityContentType.Comment => "Comments",
        CommunityContentType.Guide => "Guides",
        CommunityContentType.Listing => "Providers",
        CommunityContentType.Job => "Jobs",
        CommunityContentType.Housing => "Housing",
        CommunityContentType.Marketplace => "Marketplace",
        CommunityContentType.Event => "Events",
        CommunityContentType.Organization => "Community Groups",
        CommunityContentType.Notice => "Official notices",
        _ => type.ToString()
    };
}
