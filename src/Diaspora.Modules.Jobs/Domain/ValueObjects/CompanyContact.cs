using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Domain.ValueObjects;

/// <summary>
/// How to reach an employer, and where its careers information lives.
/// <para>
/// <b>Every URL is required to be absolute and http(s).</b> A relative or <c>javascript:</c>
/// value reaching a client that renders it as a link is a redirect vector, and an employer
/// profile is exactly the surface a fraudulent poster would use for one. Parsing with
/// <see cref="Uri"/> and pinning the scheme is the whole defence and it is cheap.
/// </para>
/// <para>
/// All four fields are optional. An employer that supplies none is perfectly legitimate — the
/// application flow runs entirely on the platform, and contact details exist for the candidate
/// who wants to check who they are dealing with, not because the process needs them.
/// </para>
/// </summary>
public sealed class CompanyContact : ValueObject
{
    private CompanyContact(
        string? email, string? phone, string? websiteUrl, string? careersUrl)
    {
        Email = email;
        Phone = phone;
        WebsiteUrl = websiteUrl;
        CareersUrl = careersUrl;
    }

    private CompanyContact() { }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public string? WebsiteUrl { get; private set; }

    /// <summary>The employer's own careers page, when it has one off-platform.</summary>
    public string? CareersUrl { get; private set; }

    /// <summary>Nothing supplied. A perfectly ordinary state.</summary>
    // Fresh per call: a shared owned instance is tracked against one owner only. See SalaryRange.
    public static CompanyContact Empty => new(null, null, null, null);

    public static Result<CompanyContact> Create(
        string? email, string? phone, string? websiteUrl, string? careersUrl)
    {
        var cleanEmail = Clean(email)?.ToLowerInvariant();

        // Deliberately shallow. Anything stricter rejects valid addresses, and the platform
        // never sends to this — it is displayed for a human to judge.
        if (cleanEmail is not null
            && (cleanEmail.Length > 320
                || cleanEmail.Count(character => character == '@') != 1
                || cleanEmail.StartsWith('@')
                || cleanEmail.EndsWith('@')))
        {
            return Result.Failure<CompanyContact>(JobErrors.EmployerContactInvalid);
        }

        var cleanPhone = Clean(phone);

        if (cleanPhone is { Length: > 40 })
        {
            return Result.Failure<CompanyContact>(JobErrors.EmployerContactInvalid);
        }

        var website = NormalizeUrl(websiteUrl);

        if (website.IsFailure)
        {
            return Result.Failure<CompanyContact>(website.Error);
        }

        var careers = NormalizeUrl(careersUrl);

        if (careers.IsFailure)
        {
            return Result.Failure<CompanyContact>(careers.Error);
        }

        return Result.Success(
            new CompanyContact(cleanEmail, cleanPhone, website.Value, careers.Value));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Email;
        yield return Phone;
        yield return WebsiteUrl;
        yield return CareersUrl;
    }

    /// <summary>
    /// Absolute http(s) only. Returns success with null for an absent value, so an optional
    /// field and an invalid one stay distinguishable.
    /// </summary>
    private static Result<string?> NormalizeUrl(string? value)
    {
        var trimmed = Clean(value);

        if (trimmed is null)
        {
            return Result.Success<string?>(null);
        }

        if (trimmed.Length > JobLimits.MaxUrl
            || !Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return Result.Failure<string?>(JobErrors.EmployerContactInvalid);
        }

        return Result.Success<string?>(uri.ToString());
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
