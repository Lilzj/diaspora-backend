using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Domain.ValueObjects;

/// <summary>
/// How to reach the landlord, and whether the platform will say.
/// <para>
/// <b>Contact details are held and disclosed separately, exactly as an address is.</b> A phone
/// number on a public listing page is harvested within the hour, and the member it belongs to
/// spends the next year fielding calls about a flat they let in a fortnight. The details are
/// stored because a confirmed viewing needs them; whether a browser sees them is
/// <see cref="ShowPublicly"/>, and the default is no.
/// </para>
/// <para>
/// <b>This is not messaging.</b> A future Communication module owns conversations between members,
/// and Housing will hand off to it the way B10 does. What lives here is the fallback for a
/// deployment that has no such module yet: a phone number the landlord chose to publish.
/// </para>
/// </summary>
public sealed class LandlordContact : ValueObject
{
    private LandlordContact(
        string? phoneNumber, string? email, string? websiteUrl, bool showPublicly)
    {
        PhoneNumber = phoneNumber;
        Email = email;
        WebsiteUrl = websiteUrl;
        ShowPublicly = showPublicly;
    }

    private LandlordContact() { }

    public string? PhoneNumber { get; private set; }

    public string? Email { get; private set; }

    public string? WebsiteUrl { get; private set; }

    /// <summary>Whether a browser may see any of this. Defaults to no.</summary>
    public bool ShowPublicly { get; private set; }

    /// <summary>Whether there is anything to show at all.</summary>
    public bool IsEmpty => PhoneNumber is null && Email is null && WebsiteUrl is null;

    /// <summary>
    /// Nothing published. A fresh instance per access: this is an owned type, and a shared static
    /// one makes EF write nulls into a second aggregate holding it.
    /// </summary>
    public static LandlordContact None => new(null, null, null, false);

    public static Result<LandlordContact> Create(
        string? phoneNumber = null,
        string? email = null,
        string? websiteUrl = null,
        bool showPublicly = false)
    {
        var phone = Clamp(phoneNumber, HousingLimits.MaxDisplayName);
        var address = Clamp(email, HousingLimits.MaxDisplayName);
        var website = Clamp(websiteUrl, HousingLimits.MaxNoteLength);

        if (address is not null && !address.Contains('@', StringComparison.Ordinal))
        {
            return Result.Failure<LandlordContact>(HousingErrors.LandlordContactInvalid);
        }

        // Absolute, and http or https only. A javascript: or data: URL rendered as a landlord's
        // website is a script the platform published on somebody else's behalf.
        if (website is not null
            && (!Uri.TryCreate(website, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)))
        {
            return Result.Failure<LandlordContact>(HousingErrors.LandlordContactInvalid);
        }

        return Result.Success(new LandlordContact(phone, address, website, showPublicly));
    }

    /// <summary>
    /// What a particular viewer may see.
    /// <para>
    /// A redacted copy rather than a flag, for the reason <c>PropertyAddress.Disclose</c> gives:
    /// a caller cannot forget a rule they were never given the chance to apply.
    /// </para>
    /// </summary>
    public LandlordContact Disclose(bool isOwner, bool isStaff, bool hasConfirmedViewing)
    {
        if (isOwner || isStaff || ShowPublicly || hasConfirmedViewing)
        {
            return this;
        }

        // The website survives redaction. It is already public, and for an agency it is the thing
        // a member most wants in order to check the landlord is real.
        return new LandlordContact(null, null, WebsiteUrl, ShowPublicly);
    }

    private static string? Clamp(string? value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > maximum ? null : trimmed;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return PhoneNumber;
        yield return Email;
        yield return WebsiteUrl;
        yield return ShowPublicly;
    }
}
