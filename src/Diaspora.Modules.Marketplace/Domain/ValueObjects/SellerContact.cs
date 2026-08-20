using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Domain.ValueObjects;

/// <summary>
/// How a seller may be reached, and how they would rather be.
/// <para>
/// <b>The details are optional and the preference is not.</b> A seller who publishes no phone
/// number and no address still has a preference, and it is <see cref="ContactPreference.InApp"/>
/// — the only setting that does not put a personal contact detail in front of every stranger who
/// opens a listing. Defaulting the other way would be a privacy decision made on the seller's
/// behalf by whoever wrote the form.
/// </para>
/// <para>
/// <b>Nothing here is verified and nothing here is used to send anything.</b> Marketplace neither
/// dials nor mails; the values are rendered by the client when the seller chose to publish them,
/// and the ownership boundary stays where B2 put it.
/// </para>
/// </summary>
public sealed class SellerContact : ValueObject
{
    private SellerContact(
        string? phone, string? email, string? website, ContactPreference preference)
    {
        Phone = phone;
        Email = email;
        Website = website;
        Preference = preference;
    }

    private SellerContact() { }

    public string? Phone { get; private set; }

    public string? Email { get; private set; }

    public string? Website { get; private set; }

    /// <summary>How the seller would rather be reached. Never null.</summary>
    public ContactPreference Preference { get; private set; }

    /// <summary>Nothing published, in-app only. What a fresh seller profile carries.</summary>
    public static SellerContact InAppOnly =>
        new(null, null, null, ContactPreference.InApp);

    public static Result<SellerContact> Create(
        string? phone,
        string? email,
        string? website,
        ContactPreference preference = ContactPreference.InApp)
    {
        if (!Enum.IsDefined(preference))
        {
            return Result.Failure<SellerContact>(MarketplaceErrors.SellerContactInvalid);
        }

        var cleanPhone = Clamp(phone);
        var cleanEmail = Clamp(email);
        var cleanWebsite = Clamp(website);

        if ((cleanPhone is null && !string.IsNullOrWhiteSpace(phone))
            || (cleanEmail is null && !string.IsNullOrWhiteSpace(email))
            || (cleanWebsite is null && !string.IsNullOrWhiteSpace(website)))
        {
            return Result.Failure<SellerContact>(MarketplaceErrors.SellerContactInvalid);
        }

        // A shape check, not a deliverability check. Refusing an address with no "@" catches the
        // typo; anything stronger would refuse valid addresses this platform's members really use.
        if (cleanEmail is not null
            && (!cleanEmail.Contains('@', StringComparison.Ordinal) || cleanEmail.StartsWith('@')))
        {
            return Result.Failure<SellerContact>(MarketplaceErrors.SellerContactInvalid);
        }

        // A preference for a channel the seller published nothing for would render a contact
        // button that does nothing.
        var effective = preference switch
        {
            ContactPreference.Phone when cleanPhone is null => ContactPreference.InApp,
            ContactPreference.Email when cleanEmail is null => ContactPreference.InApp,
            _ => preference
        };

        return Result.Success(
            new SellerContact(cleanPhone, cleanEmail, cleanWebsite, effective));
    }

    private static string? Clamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > MarketplaceLimits.MaxContactValue ? null : trimmed;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Phone;
        yield return Email;
        yield return Website;
        yield return Preference;
    }
}
