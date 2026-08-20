using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Domain.ValueObjects;

/// <summary>
/// Who the seller says they are.
/// <para>
/// <b><see cref="DisplayName"/> is what buyers see and <see cref="LegalName"/> is what Trust
/// reviews, and they are separate fields because they are separate claims.</b> A shop trading as
/// "Mama Afrika Foods" may be registered as something else entirely, and a verification that
/// reviewed the trading name would have verified nothing. The legal name and the registration
/// number go to Trust in the opaque metadata bag; neither is rendered to a buyer.
/// </para>
/// <para>
/// <b>Nothing here says "verified".</b> The declared <see cref="Kind"/> is the seller's own
/// statement; the badge is Trust's, and lives on the trust mark.
/// </para>
/// </summary>
public sealed class SellerIdentity : ValueObject
{
    private SellerIdentity(
        string displayName,
        string? legalName,
        string? registrationNumber,
        SellerKind kind,
        string? bio)
    {
        DisplayName = displayName;
        LegalName = legalName;
        RegistrationNumber = registrationNumber;
        Kind = kind;
        Bio = bio;
    }

    private SellerIdentity() { }

    /// <summary>The trading name buyers see.</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>The registered name, for Trust. Never rendered to a buyer.</summary>
    public string? LegalName { get; private set; }

    /// <summary>A company number, for Trust. Never parsed here, never rendered.</summary>
    public string? RegistrationNumber { get; private set; }

    public SellerKind Kind { get; private set; }

    /// <summary>The seller's own words about themselves. Plain text.</summary>
    public string? Bio { get; private set; }

    /// <summary>What goes to Trust: the legal name where there is one, the trading name otherwise.</summary>
    public string VerifiableName => LegalName ?? DisplayName;

    public static Result<SellerIdentity> Create(
        string? displayName,
        string? legalName,
        string? registrationNumber,
        SellerKind kind,
        string? bio = null)
    {
        if (!Enum.IsDefined(kind))
        {
            return Result.Failure<SellerIdentity>(MarketplaceErrors.SellerKindInvalid);
        }

        var name = Clamp(displayName, MarketplaceLimits.MaxSellerName);

        if (name is null)
        {
            return Result.Failure<SellerIdentity>(MarketplaceErrors.SellerNameInvalid);
        }

        var legal = Clamp(legalName, MarketplaceLimits.MaxSellerName);
        var registration = Clamp(registrationNumber, MarketplaceLimits.MaxRegistrationNumber);
        var about = Clamp(bio, MarketplaceLimits.MaxSellerBio);

        if ((legal is null && !string.IsNullOrWhiteSpace(legalName))
            || (registration is null && !string.IsNullOrWhiteSpace(registrationNumber))
            || (about is null && !string.IsNullOrWhiteSpace(bio)))
        {
            return Result.Failure<SellerIdentity>(MarketplaceErrors.SellerNameInvalid);
        }

        return Result.Success(new SellerIdentity(name, legal, registration, kind, about));
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
        yield return DisplayName;
        yield return LegalName;
        yield return RegistrationNumber;
        yield return Kind;
        yield return Bio;
    }
}
