using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Domain.ValueObjects;

/// <summary>
/// How to reach an organizer or an organization.
/// <para>
/// <b>WhatsApp is a first-class field rather than a note</b>, and that is a product judgement about
/// this platform's members rather than a technical one: across most of the diaspora communities
/// this serves, a WhatsApp number is the contact somebody will actually use, and folding it into a
/// generic "phone" loses the distinction between a number you may call and one you may message.
/// </para>
/// <para>
/// Every field is optional. An event whose only contact is the organization page behind it is
/// perfectly normal, and requiring a phone number would push organizers into publishing a personal
/// one.
/// </para>
/// </summary>
public sealed class ContactDetails : ValueObject
{
    private ContactDetails(
        string? phone, string? whatsApp, string? email, string? website, string? contactName)
    {
        Phone = phone;
        WhatsApp = whatsApp;
        Email = email;
        Website = website;
        ContactName = contactName;
    }

    private ContactDetails() { }

    /// <summary>Nothing at all. The state most drafts start in.</summary>
    public static ContactDetails Empty => new(null, null, null, null, null);

    public string? Phone { get; private set; }

    public string? WhatsApp { get; private set; }

    public string? Email { get; private set; }

    public string? Website { get; private set; }

    /// <summary>Who to ask for. Optional, and often the only thing an organizer wants to publish.</summary>
    public string? ContactName { get; private set; }

    public bool IsEmpty =>
        Phone is null && WhatsApp is null && Email is null && Website is null;

    public static Result<ContactDetails> Create(
        string? phone = null,
        string? whatsApp = null,
        string? email = null,
        string? website = null,
        string? contactName = null)
    {
        var trimmedEmail = Trim(email, CommunityLifeLimits.MaxDisplayName);

        // Shape only. Deliverability is not something a regular expression can decide, and a
        // stricter rule here would refuse addresses that work.
        if (trimmedEmail is not null
            && (!trimmedEmail.Contains('@', StringComparison.Ordinal)
                || trimmedEmail.StartsWith('@')
                || trimmedEmail.EndsWith('@')))
        {
            return Result.Failure<ContactDetails>(CommunityLifeErrors.ContactInvalid);
        }

        var trimmedWebsite = Trim(website, CommunityLifeLimits.MaxUrl);

        if (trimmedWebsite is not null && !IsAbsoluteHttpUrl(trimmedWebsite))
        {
            return Result.Failure<ContactDetails>(CommunityLifeErrors.ContactInvalid);
        }

        return Result.Success(new ContactDetails(
            Trim(phone, CommunityLifeLimits.MaxDisplayName),
            Trim(whatsApp, CommunityLifeLimits.MaxDisplayName),
            trimmedEmail,
            trimmedWebsite,
            Trim(contactName, CommunityLifeLimits.MaxDisplayName)));
    }

    /// <summary>
    /// Whether a string is a URL the client may safely render as a link. Scheme-checked rather
    /// than merely parsed, because <c>javascript:</c> parses perfectly well as a URI.
    /// </summary>
    internal static bool IsAbsoluteHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string? Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Phone;
        yield return WhatsApp;
        yield return Email;
        yield return Website;
        yield return ContactName;
    }
}
